---
phase: 04-save-versioning-migration
plan: 01
subsystem: data-persistence
tags: [unity, newtonsoft-json, versioning, migration, nunit]

requires:
  - phase: 01-foundation-core
    provides: DSMSlot atomic Save/Load (_ioGate, ReplaceFile), DSMSerializer, DSMSlotManager slot lifecycle
  - phase: 02-encryption-hardening-key-validation-rotation
    provides: encrypt-aware atomic write path (ReplaceFile, tmp-then-rename), colocated-exception precedent (DSMRotationInterruptedException)
  - phase: 03-schema-validation
    provides: migrated output remains schema-correct (rename migrations target the schema key)
provides:
  - DSMSaveEnvelope — { version, data } envelope Wrap/TryUnwrap with legacy-flat detection (no integer version + object data => v1, data=root)
  - DSMSaveVersionException (value-free message: offending vs current version numbers only)
  - IDSMMigration — per-step transform contract (FromVersion + Migrate(JObject))
  - DSMMigrationRunner — contiguity-validating chain, CurrentVersion derived from the chain, fail-closed on future/broken versions, static Empty
  - DSMSerializer.SerializeEnvelope(dict, version, prettyPrint)
  - DSMSlot 6-arg ctor (optional DSMMigrationRunner) + gate-held WriteJsonToDisk/WriteJsonToDiskAsync cores + lazy migrate-on-load + write-back
  - DSMSlotManager 2-arg ctor (optional DSMMigrationRunner threaded into every slot)
  - DSM.RegisterMigration / DSM.ClearMigrations static migration registry
affects: [05-performance-reactivity-editor-tooling (EDIT-01 version/migration status UI reads these APIs)]

tech-stack:
  added: []
  patterns:
    - "Versioned envelope with a single home (DSMSaveEnvelope.Wrap/TryUnwrap) — on-disk version separate from the game payload under `data`"
    - "CurrentVersion derived from the registered migration chain (max(FromVersion)+1), never a separately-maintained constant, so on-disk version and chain cannot drift"
    - "Gate-held write-core extracted from Save/SaveAsync so the load-time write-back reuses the SAME encrypt-aware atomic path WITHOUT re-acquiring the non-reentrant _ioGate (deadlock-safe)"
    - "Fail-closed on future/unknown save versions — throw DSMSaveVersionException, leave the file byte-for-byte untouched (no write-back, no seed-defaults)"
    - "Colocated exception (DSMSaveVersionException beside DSMSaveEnvelope), mirroring DSMRotationInterruptedException beside DSMSlotManager"

key-files:
  created:
    - Runtime/DSMSaveEnvelope.cs
    - Runtime/IDSMMigration.cs
    - Runtime/DSMMigrationRunner.cs
    - Tests/Editor/DSMMigrationTests.cs
    - Tests/Editor/TestFixtures/slot-legacy-v1.json
    - Tests/Editor/TestFixtures/slot-v2.json
    - Tests/Editor/TestFixtures/slot-v3.json
  modified:
    - Runtime/DSMSerializer.cs
    - Runtime/DSMSlot.cs
    - Runtime/DSMSlotManager.cs
    - Runtime/DSM.cs
    - Tests/Editor/DSMSlotAtomicSaveTests.cs
    - Tests/Editor/DSMSlotConcurrencyTests.cs
    - Tests/Editor/DSMSlotDebounceTests.cs

key-decisions:
  - "The load-time write-back reuses an extracted gate-held write core (WriteJsonToDisk/WriteJsonToDiskAsync), never Save()/SaveAsync() — _ioGate is a non-reentrant SemaphoreSlim(1,1) and re-acquiring it inside Load/LoadAsync would deadlock the slot (T-04-02). The encrypt/atomic branch (`if (_config.Encrypt) ... else ...` + ReplaceFile) was relocated verbatim, so a migrated encrypted slot is re-encrypted, never written as plaintext (T-04-01)."
  - "A future/unknown envelope version (> CurrentVersion) or a broken migration chain fails closed: DSMMigrationRunner.Migrate throws DSMSaveVersionException, and Load/LoadAsync let it propagate — no write-back, no seed-defaults — so a newer app's save is never silently truncated or destroyed (T-04-03)."
  - "CurrentVersion is derived from the migration chain (1 when empty, else max(FromVersion)+1), not stored as a separate constant, so the on-disk version and the migration chain can never drift apart."
  - "Legacy pre-versioning flat saves are detected by TryUnwrap (no integer `version` + object `data`), treated as SaveVersion 1, migrated forward, and re-persisted in envelope form on first load — backward compatible."
  - "DSMSlot/DSMSlotManager gained OPTIONAL trailing runner params (default null => DSMMigrationRunner.Empty => v1 passthrough), so every existing 5-arg new DSMSlot(...) / 1-arg new DSMSlotManager(config) call site compiles unchanged."

requirements-completed: [MIGR-01, MIGR-02, MIGR-03, TEST-06]

coverage:
  - id: D1
    description: "Every slot file is written as a { version, data } envelope; the version lives outside the payload"
    requirement: MIGR-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Save_WritesVersionedEnvelope"
        status: unknown
    human_judgment: true
    rationale: "Project constraint (.claude/CLAUDE.md) forbids running Unity EditMode tests from this agent — batchmode has deadlocked this project. dotnet build DMS.Runtime.csproj passed (0 errors) and the on-disk shape is asserted by the test, but the human must run Unity Test Runner to confirm."
  - id: D2
    description: "An older-version slot migrates to CurrentVersion only when loaded (not a startup bulk pass) and the migrated envelope is written back immediately"
    requirement: MIGR-03
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_LegacyFlatFile_MigratesToCurrentVersion"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_LegacyFlatFile_WritesBackEnvelopeImmediately"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_V2Envelope_MigratesStepwiseToV3"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint. Migration runs inside the single loaded slot's Load/LoadAsync; DSMSlotManager does not iterate all slots."
  - id: D3
    description: "Migrations are composable per-step transforms (IDSMMigration + DSMMigrationRunner); a renamed/removed key is explicitly remapped, not silently defaulted"
    requirement: MIGR-02
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_RenamedKey_RemappedNotDefaulted"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Runner_CurrentVersion_DerivedFromChain"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Runner_BrokenChain_Throws"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint."
  - id: D4
    description: "A TestFixtures/ set of versioned sample saves exists; regression tests confirm each migrates to the expected current-version payload"
    requirement: TEST-06
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_CommittedFixture_MigratesToCurrentPayload"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint. Three fixtures (legacy-v1 flat, v2 envelope, v3 envelope) are committed and validated as JSON by the automated gate; the fixture-load assertions run in the Test Runner."
  - id: D5
    description: "Future/unknown version fails closed with the file untouched (no data loss); encryption/atomicity preserved on write-back"
    requirement: MIGR-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMMigrationTests.cs#Load_FutureVersion_ThrowsAndLeavesFileUntouched"
        status: unknown
      - kind: source-grep
        ref: "DSMSlot.Load/LoadAsync call WriteJsonToDisk(Async), never Save()/SaveAsync() (T-04-02); encrypt branch retained verbatim (T-04-01)"
        status: pass
    human_judgment: true
    rationale: "The grep assertion (no Save/SaveAsync call inside Load bodies; encrypt branch preserved) passed automatically. The throw-and-bytes-unchanged behavior is asserted by the test and needs the human Test Runner pass."

duration: ~18min
completed: 2026-07-21
status: complete
---

# Phase 4 Plan 1: Save Versioning + Migration Summary

**Every slot file is now a `{ version, data }` envelope; an out-of-date or legacy-flat slot migrates forward through a composable per-step `IDSMMigration` chain lazily on load and is re-persisted immediately via a gate-held, encrypt-aware write core — future/unknown versions fail closed with the file untouched.**

## Performance

- **Duration:** ~18 min
- **Completed:** 2026-07-21
- **Tasks:** 5 completed (RED spec+fixtures, 3 GREEN implementation, existing-test audit)
- **Files:** 12 (7 created, 7 modified) — note DSMSlot.cs counts once though touched across tasks

## Accomplishments
- **DSMSaveEnvelope** gives the `{ version, data }` envelope a single home: `Wrap` builds it, `TryUnwrap` returns the version+payload for a real envelope and falls back to `(version=1, data=root)` for a pre-versioning flat object. `DSMSaveVersionException` is colocated (value-free message).
- **IDSMMigration + DSMMigrationRunner** express migrations as per-step transforms. The runner sorts by `FromVersion`, validates a contiguous `1..N` chain (throws on gap/dup/first≠1), derives `CurrentVersion` from the chain, no-ops at current, clamps legacy (<1) to 1, and fails closed on a future version.
- **DSMSlot** wiring: optional 6th ctor param `DSMMigrationRunner`; `Save/SaveAsync` now emit the envelope at `runner.CurrentVersion` and delegate the encrypt/atomic write to extracted **gate-held** `WriteJsonToDisk`/`WriteJsonToDiskAsync` cores; `Load/LoadAsync` unwrap → migrate the single slot → write the upgraded envelope back **while `_ioGate` is still held** (never `Save`/`SaveAsync`).
- **Threading:** `DSMSlotManager` gained an optional runner passed into every `new DSMSlot(...)`; `DSM` added a `RegisterMigration`/`ClearMigrations` static registry and builds `new DSMMigrationRunner(s_migrations)` in `Configure`/`Initialize`.
- **Tests:** new EditMode `DSMMigrationTests` (11 `[Test]` + a 3-case fixture regression) plus three committed `TestFixtures/` saves; existing on-disk-shape assertions in three slot fixtures re-pointed at the envelope `data`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Failing migration spec + versioned fixtures (RED)** — `12aa44b` (test)
2. **Task 2: Versioned save envelope + DSMSaveVersionException (MIGR-01)** — `ff6c28d` (feat)
3. **Task 3: IDSMMigration + DSMMigrationRunner (MIGR-02)** — `23d9c16` (feat)
4. **Task 4: Lazy migrate-on-load + gate-safe write-back, thread runner (MIGR-01, MIGR-03)** — `81aee6e` (feat)
5. **Task 5: Re-point existing slot on-disk assertions at the envelope** — `c6ca635` (test)

## TDD Gate Compliance

Phase mode is `mvp` with TDD. RED gate confirmed: `12aa44b test(04-01): failing migration spec + versioned fixtures (RED)` — the test assembly deliberately references the not-yet-existing envelope/migration symbols and cannot compile until Tasks 2–4. GREEN landed after RED (`ff6c28d`, `23d9c16`, `81aee6e`), each verified by `dotnet build DMS.Runtime.csproj` (0 errors) plus grep symbol assertions. No REFACTOR commit needed — the implementation matched the plan on first pass.

## Files Created/Modified
- `Runtime/DSMSaveEnvelope.cs` — New: `DSMSaveEnvelope` (Wrap/TryUnwrap, VersionKey/DataKey/LegacyVersion) + colocated `DSMSaveVersionException`
- `Runtime/IDSMMigration.cs` — New: per-step migration contract
- `Runtime/DSMMigrationRunner.cs` — New: contiguity-validating chain, derived `CurrentVersion`, `Migrate`, static `Empty`
- `Runtime/DSMSerializer.cs` — Added `SerializeEnvelope(dict, version, prettyPrint)` beside the existing flat `Serialize`
- `Runtime/DSMSlot.cs` — Optional 6th ctor param + `_migrationRunner`; extracted `WriteJsonToDisk`/`WriteJsonToDiskAsync` gate-held cores; `SerializeSnapshot` now envelopes; `Load`/`LoadAsync` unwrap+migrate+gate-safe write-back (+`ApplyPayload` helper)
- `Runtime/DSMSlotManager.cs` — Optional 2nd ctor param threaded into `GetOrCreateSlot`
- `Runtime/DSM.cs` — `RegisterMigration`/`ClearMigrations` registry; runner built in `Configure`/`Initialize`
- `Tests/Editor/DSMMigrationTests.cs` — New EditMode fixture (11 `[Test]` + 3-case fixture regression)
- `Tests/Editor/TestFixtures/slot-legacy-v1.json`, `slot-v2.json`, `slot-v3.json` — Committed versioned sample saves
- `Tests/Editor/DSMSlotAtomicSaveTests.cs`, `DSMSlotConcurrencyTests.cs`, `DSMSlotDebounceTests.cs` — On-disk-shape assertions re-pointed at the envelope `data`; `DSMSlotLoadRobustnessTests.cs` left unchanged (malformed→seed path only)

## Decisions Made
- Reused the extracted gate-held write core for the load-time write-back so `_ioGate` (non-reentrant) is never re-acquired — the single design choice that mitigates both the deadlock (T-04-02) and the plaintext-on-migration (T-04-01) threats, since the `if (_config.Encrypt) ... else ...` + `ReplaceFile` branch was relocated verbatim.
- Derived `CurrentVersion` from the registered chain rather than a standalone constant, eliminating on-disk/chain drift by construction.
- Kept the new ctor params optional and trailing so no existing call site (tests included) needed changes.
- Left `DSMSlotLoadRobustnessTests` untouched: it only writes malformed JSON to force the seed-defaults path, which is unaffected by the envelope format (malformed JSON still throws `JObject.Parse` → seed defaults; a valid flat file now loads as legacy v1).

## Deviations from Plan

None in code — plan executed as written. One environment-only adjustment (not a code deviation): the Unity-auto-generated, gitignored `DMS.Runtime.csproj` did not list the three new Runtime files (Unity regenerates this on domain reload, which cannot run here). Added the missing `<Compile Include>` lines locally so `dotnet build DMS.Runtime.csproj` could verify the Runtime assembly compiles; the csproj is gitignored and was not committed.

## Issues Encountered
The first `dotnet build` after Task 2 failed with `CS0103: DSMSaveEnvelope does not exist` — the new file was not in the gitignored Unity csproj's explicit `<Compile Include>` list. Resolved by adding the Compile entries locally (see Deviations). No other issues.

## Open Human-Verification Item (Unity Test Runner)

**This is the acceptance gate for MIGR-01/MIGR-02/MIGR-03 + TEST-06 and has NOT been run by this agent** — per `.claude/CLAUDE.md`, Unity tests (EditMode/PlayMode, batchmode, `-runTests`) must never be run by the executor; Unity batchmode has previously deadlocked this project and hit Unix-domain-socket path failures. A migration bug can manifest as a deadlock on load (the exact failure the batchmode ban exists to avoid), so this must be run interactively.

**What the human must do:**
1. Open Unity Editor → Window → General → Test Runner → EditMode tab.
2. Run the `DSMMigrationTests` fixture and confirm all pass — especially: `Save_WritesVersionedEnvelope`, `Load_LegacyFlatFile_MigratesToCurrentVersion`, `Load_LegacyFlatFile_WritesBackEnvelopeImmediately`, `Load_V2Envelope_MigratesStepwiseToV3`, `Load_CurrentVersion_NotRewritten`, `Load_FutureVersion_ThrowsAndLeavesFileUntouched` (must throw `DSMSaveVersionException`, file bytes unchanged, **no hang**), `Runner_BrokenChain_Throws`, and the three `Load_CommittedFixture_MigratesToCurrentPayload` cases.
3. Re-run the FULL existing suite (Phase 1/2/3 fixtures — `DSMSlotLoadRobustnessTests`, `DSMSlotAtomicSaveTests`, `DSMSlotConcurrencyTests`, `DSMSlotDebounceTests`, `DSMKeyRotationTests`, `DSMSchemaValidationTests`, encryption tests) to confirm no regression from the envelope format change + the Task 5 assertion audit.
4. Report pass/fail; any failure is a bug against this plan's implementation, not a plan deviation.

## Next Phase Readiness
`DSMSaveEnvelope`, `IDSMMigration`, `DSMMigrationRunner`, and the `DSM.RegisterMigration` registry are available for Phase 5's Editor tooling (EDIT-01 — surface each slot's save version and migration status). No blockers for subsequent phases; the only open item is the human Unity Test Runner pass documented above.

---
*Phase: 04-save-versioning-migration*
*Completed: 2026-07-21*

## Self-Check: PASSED

- FOUND: Runtime/DSMSaveEnvelope.cs
- FOUND: Runtime/IDSMMigration.cs
- FOUND: Runtime/DSMMigrationRunner.cs
- FOUND: Tests/Editor/DSMMigrationTests.cs
- FOUND: Tests/Editor/TestFixtures/slot-legacy-v1.json
- FOUND: Tests/Editor/TestFixtures/slot-v2.json
- FOUND: Tests/Editor/TestFixtures/slot-v3.json
- FOUND: .planning/phases/04-save-versioning-migration/04-01-SUMMARY.md
- FOUND commit: 12aa44b
- FOUND commit: ff6c28d
- FOUND commit: 23d9c16
- FOUND commit: 81aee6e
- FOUND commit: c6ca635
