# Walking Skeleton — DataSaveManager (DSM)

**Phase:** 1
**Generated:** 2026-07-08

## Capability Proven End-to-End

> One sentence: the smallest user-visible capability that exercises the full stack.

A developer can run the DSM test suite in Unity's EditMode Test Runner and see a concurrency regression test — 100 parallel `Set()` calls followed by a real `Save()`→`Load()` disk roundtrip — pass green, proving the test-infrastructure + synchronization + persistence stack works end to end.

Note: this is a hardening milestone for an existing, working library, not a greenfield app. "Walking skeleton" here means the thinnest end-to-end proof that the phase's three pillars — (1) a running EditMode test harness, (2) a real thread-safety fix on shared slot state, and (3) verification by a green final-state-correctness test — are wired together. Atomic I/O, the debounce rewrite, and input validation are layered on in the two subsequent slices without changing these decisions.

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Test framework | Unity Test Framework (NUnit) EditMode-only | D-06: `DSMSlot` has no MonoBehaviour lifecycle dependency; `async Task`/`UniTask` behavior runs fine in EditMode. Faster, simpler CI than PlayMode/`[UnityTest]`. |
| Test assemblies | `Tests/Editor/DMS.Tests.Editor.asmdef` + `Tests/Runtime/DMS.Tests.Runtime.asmdef`, both `includePlatforms: ["Editor"]` | TEST-01 + D-06. `Tests/Runtime` is an asmdef referencing the Runtime assembly — NOT PlayMode execution; both run in Edit Mode. |
| In-memory sync primitive | Plain `lock (object)` around `_data` dictionary mutations | D-05 sync half. Matches the only existing thread-safety precedent in the codebase (`DSMWatcher.cs` `lock (_lock)`), keeps the hot `Set`/`Get` path fully synchronous. |
| Slot-dictionary sync primitive | Separate coarse `lock (_slotsLock)` scoped to add/remove-slot ops only | D-04. Per-slot granularity — a single global lock would serialize all slots for no benefit. |
| Async I/O sync primitive | Per-slot `SemaphoreSlim(1,1)` I/O gate around `Load`/`Save`/`SaveAsync`/`LoadAsync` | D-05 async half. A plain `lock` cannot be held across `await`; the split keeps the sync path lock-cheap while gating await-spanning I/O. (Landed in Slice 2.) |
| Config injection for tests | `DSM.Configure(config)` seam + reflection-set private `DSMConfig` fields via a `DSMTestConfig` fixture builder | ARCHITECTURE.md documents `Configure()` as the intended test-injection seam; `DSMConfig` backing fields (`_autoSave`, `_autoSaveDebounce`, `_encrypt`, `_savePath`) are private `[SerializeField]`, so tests set them by reflection. |
| Directory layout | `Tests/Editor/*` and `Tests/Runtime/*` mirror the existing `Runtime/` + `Editor/` package layout | STRUCTURE.md package convention; test asmdefs modeled on existing `DMS.Runtime.asmdef` / `DSM.Editor.asmdef`. |

## Stack Touched in Phase 1

- [x] Project scaffold — two test asmdefs + smoke tests proving the Test Runner discovers and runs DSM tests (Slice 1)
- [x] Routing equivalent (n/a for a library) — replaced by the public API surface already in `DSM.cs`/`DSMSlot.cs` being exercised by tests
- [x] "Database" read AND write — real `DSMSlot.Save()` to disk + `DSMSlot.Load()` back, asserted in the skeleton roundtrip test (Slice 1); made atomic in Slice 2
- [x] UI wired to logic — `DSMRuntimePanel.BuildWidgets()` widget-missing-`IDSMWidget` warning (Slice 3, the phase's only UI-adjacent change; it is a log line, not a visual surface — no UI-SPEC required)
- [x] Local full-stack run — Unity EditMode Test Runner (`/Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity -runTests -batchmode -testPlatform EditMode`) executes the whole suite green

## Out of Scope (Deferred to Later Slices / Phases)

> Explicit so later phases do not re-litigate Phase 1's minimalism.

- Encryption key validation, Encrypt-then-MAC, key rotation → Phase 2 (BUGS-02, ENC-01, ENC-02)
- Schema type validation on `Set`/`Get` → Phase 3 (SCHM-01, SCHM-02)
- Save versioning envelope + lazy migration → Phase 4 (MIGR-01..03)
- Batched watcher notifications, `DSMManagerWindow` split/caching, UniTask commit-pin, Editor version/rotate UI → Phase 5 (WATCH-01, PERF-01..04, EDIT-01, BUGS-01)
- PlayMode / `[UnityTest]` tests → not used this phase (D-06)
- Any change to the `DSMConfig` public API or save-file format for back-compat → out of scope (breaking changes accepted this milestone)

## Subsequent Slice Plan (within Phase 1)

Each slice adds one vertical capability on top of the skeleton without changing the decisions above:

- **Slice 1 (Plan 01, Wave 1) — Skeleton:** test asmdefs + smoke + failing concurrency regression test + `_data`/`_slots` locks → test goes green.
- **Slice 2 (Plan 02, Wave 2):** `SemaphoreSlim` I/O gate + atomic temp-write-then-rename (CONC-03) + debounce/CTS race rewrite (CONC-02/D-01), each shipped with its regression test.
- **Slice 3 (Plan 03, Wave 3):** strict slot-name validation (BUGS-03/D-03) + malformed-JSON load fallback (D-02) + widget-missing-component warning (BUGS-04), each shipped with its invalid-input test; closes with the human-verify green-suite gate.

## Subsequent Phase Plan (across the milestone)

- Phase 2: encryption keys validated everywhere, encrypted saves tamper-evident, key rotation never loses data.
- Phase 3: `Set`/`Get` type-checked against `DSMConstant` codegen schema.
- Phase 4: versioned save envelope with lazy per-slot migration on load.
- Phase 5: batched watchers, Editor window decomposition/caching, and new version/migration/rotate-key Editor UI.
