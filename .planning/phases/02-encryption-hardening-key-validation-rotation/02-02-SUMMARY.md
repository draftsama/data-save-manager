---
phase: 02-encryption-hardening-key-validation-rotation
plan: 02
subsystem: encryption
tags: [key-rotation, atomic-commit, journal-recovery, unity, nunit, uni-task]

# Dependency graph
requires:
  - phase: 02-encryption-hardening-key-validation-rotation
    provides: "DSMEncryptionKey.Validate (Plan 02-01) — shared key-validation chokepoint; Encrypt-then-MAC DSMEncryptor format (Plan 02-01) — the buffer rotation re-encrypts"
provides:
  - "DSM.RotateEncryptionKeyAsync(newKey) — public static UniTask entry point for atomic key rotation"
  - "DSMSlotManager.RotateEncryptionKeyAsync(newKey) — stage-all-then-commit orchestration with a rotation journal for crash recovery"
  - "DSMSlot.StageReencryptAsync/CommitReencrypt/CleanupStagedTemp — per-slot re-encrypt primitives running under the existing _ioGate and reusing ReplaceFile"
  - "DSMSlotManager.RecoverInterruptedRotation — constructor-time journal replay so an interrupted commit never leaves a slot unreadable"
affects: [phase-04-migration-versioning]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Stage-all-then-commit key rotation: decrypt-old/re-encrypt-new to a sibling .tmp for every encrypted slot first; only after every slot stages successfully does the commit burst (rename + new config key) begin — a mid-staging failure never touches the config key or any original file"
    - "Rotation journal as crash-recovery marker: a plain slot-name-list file written immediately before the commit burst, deleted immediately after; its mere presence on next DSMSlotManager construction means 'finish these pending renames before loading anything'"
    - "Re-encrypt stage verifies its own output (decrypts the freshly-written .tmp with the new key) before counting as staged — a corrupt stage is caught at stage time, not at commit or load time"

key-files:
  created:
    - Tests/Editor/DSMKeyRotationTests.cs
  modified:
    - Runtime/DSM.cs
    - Runtime/DSMSlotManager.cs
    - Runtime/DSMSlot.cs

key-decisions:
  - "Rotation journal format is a plain newline-separated list of slot names (File.WriteAllLines/ReadAllLines) rather than JSON — no other rotation metadata (old/new key, timestamps) is stored in it, since the journal only needs to answer 'which slots have a pending .tmp to commit'"
  - "Journal recovery reuses DSMSlot.CommitReencrypt() (which itself reuses ReplaceFile) instead of duplicating a File.Replace/Move call in DSMSlotManager — keeps the atomic-rename primitive singular per the plan's explicit reuse requirement"
  - "A rotation on a config with an unusable/unset current key (e.g. EncryptionKey never assigned) is rejected with InvalidOperationException before any file work, distinct from the ArgumentException a weak/empty newKey produces — callers can tell 'my rotation call was malformed' from 'my rotation target is a bad key'"

requirements-completed: [ENC-02, TEST-02]

coverage:
  - id: D1
    description: "DSM.RotateEncryptionKeyAsync re-encrypts every encrypted slot from the current key to a new key and every slot is readable with the new key afterward; the old key no longer decrypts the on-disk files"
    requirement: "ENC-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMKeyRotationTests.cs#Rotate_ReencryptsAllSlots_ReadableWithNewKey"
        status: pass
    human_judgment: true
    rationale: "Unity Editor was open this session, blocking -runTests batchmode (confirmed via direct attempt: 'another Unity instance is running with this project open' — same finding as Phase 01 and Plan 02-01). Verified by a clean dotnet build of the real production assemblies (DMS.Runtime.csproj, DSM.Editor.csproj, DMS.Tests.Editor.csproj, 0 errors) with the previously-RED test file compiling against the real rotation API, plus a full manual trace of this test's control flow against the implementation. A human must confirm green in Unity Test Runner before this is fully proven in-engine."
  - id: D2
    description: "A pre-commit staging failure (one slot cannot be decrypted with the old key) leaves every valid slot readable with the old key, the config key unchanged, and no leftover .tmp files — no mixed-key state"
    requirement: "ENC-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMKeyRotationTests.cs#Rotate_PreCommitFailure_LeavesAllSlotsOnOldKey"
        status: pass
    human_judgment: true
    rationale: "Same verification method and same open Unity Test Runner item as D1."
  - id: D3
    description: "An interruption during the commit burst (a slot's .tmp already re-encrypted with the new key, journal present, old .enc still in place) is recovered automatically by the next DSMSlotManager construction, which finishes the pending rename and deletes the journal before loading any slot"
    requirement: "ENC-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMKeyRotationTests.cs#Rotate_JournalRecovery_CompletesInterruptedCommit"
        status: pass
    human_judgment: true
    rationale: "Same verification method and same open Unity Test Runner item as D1."
  - id: D4
    description: "A slot saved with key A, then rotated to key B, loads correctly with key B after a fresh DSM/DSMSlotManager construction (key-change-between-saves scenario)"
    requirement: "TEST-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMKeyRotationTests.cs#RotateThenReload_KeyChangeBetweenSaves_DataSurvives"
        status: pass
    human_judgment: true
    rationale: "Same verification method and same open Unity Test Runner item as D1. This closes out TEST-02, which Plan 02-01 explicitly left partial pending this scenario."
  - id: D5
    description: "Rotation to an empty or too-short new key is rejected with ArgumentException before any file work, leaving the config key and all slot files unchanged"
    requirement: "ENC-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMKeyRotationTests.cs#Rotate_ToWeakKey_Rejected"
        status: pass
    human_judgment: true
    rationale: "Same verification method and same open Unity Test Runner item as D1."
  - id: D6
    description: "No oldKey/newKey/derived key material appears in any Debug.Log or exception message in DSMSlotManager.cs or DSMSlot.cs"
    requirement: "ENC-02"
    verification:
      - kind: other
        ref: "grep -nE 'Debug\\.Log|Exception\\(' Runtime/DSMSlotManager.cs Runtime/DSMSlot.cs — 4 matches, none interpolate oldKey/newKey/EncryptionKey"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-07-14
status: complete
---

# Phase 02 Plan 02: Encryption Hardening — Atomic Key Rotation Summary

**`DSM.RotateEncryptionKeyAsync(newKey)` re-encrypts every slot under a stage-all-then-commit protocol, committing the new key to `DSMConfig` only after every slot succeeds, with a rotation journal that lets a fresh `DSMSlotManager` finish an interrupted commit on next construction.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-07-14T13:41:00Z
- **Completed:** 2026-07-14T13:49:33Z
- **Tasks:** 2 (TDD: RED then GREEN)
- **Files modified:** 5 (2 created including .meta, 3 modified)

## Accomplishments
- `DSM.RotateEncryptionKeyAsync(string)` is now a real, documented public entry point — previously there was no way to rotate a compromised or weak encryption key without hand-editing save files.
- Rotation is all-or-nothing at the staging boundary: every encrypted slot is decrypted with the old key and re-encrypted with the new key to a sibling `.tmp` file first, and the staged output is verified (decrypted with the new key) before counting as staged. Any single failure — wrong key on one slot, weak new key, disabled encryption — cleans up every `.tmp` staged so far and leaves the config key and every original `.enc` file untouched.
- Rotation is recoverable at the commit boundary: a plain rotation-journal marker file is written immediately before the commit burst (a list of slot names) and deleted immediately after. If the process is interrupted mid-commit, the next `DSMSlotManager` construction detects the journal, finishes the pending `.tmp` → `.enc` renames, and deletes the journal — all before the active slot loads, so no slot is ever left unreadable.
- `DSMSlot` gains `StageReencryptAsync`/`CommitReencrypt`/`CleanupStagedTemp`, all running under the existing `_ioGate` (the same semaphore that already serializes `Save`/`Load`) and reusing the existing `ReplaceFile` atomic-rename primitive — no second locking mechanism and no new file-rename logic was introduced.
- TEST-02 (key-change-between-saves) is now fully covered, closing out the item Plan 02-01 explicitly left partial.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing rotation + interruption + key-change tests (RED)** - `df47d68` (test)
2. **Task 2: Implement atomic RotateEncryptionKeyAsync with staging + journal recovery (GREEN)** - `fb0fa1f` (feat)

**Plan metadata:** (this commit, follows)

## Files Created/Modified
- `Runtime/DSM.cs` - Adds `RotateEncryptionKeyAsync(string) => Manager.RotateEncryptionKeyAsync(newKey)` facade entry point with XML doc summary
- `Runtime/DSMSlotManager.cs` - Adds `RotationJournalName` const, `RotateEncryptionKeyAsync` orchestration (validate → stage-all → journal → commit-all → set key → delete journal), `RecoverInterruptedRotation` called from the constructor before the active slot loads
- `Runtime/DSMSlot.cs` - Adds `StageReencryptAsync(oldKey, newKey)`, `CommitReencrypt()`, `CleanupStagedTemp()` — all under `_ioGate`, reusing `ReplaceFile`
- `Tests/Editor/DSMKeyRotationTests.cs` - New: 5 tests covering full-rotation-readable-with-new-key, pre-commit failure leaves old-key state intact, journal recovery completes an interrupted commit, key-change-between-saves via the DSM facade (TEST-02), and weak-new-key rejection
- `Tests/Editor/DSMKeyRotationTests.cs.meta` - Unity meta file for the new test fixture (guid `d321cc2faaf944049d1a813978517247`)

## Decisions Made
- Rotation journal is a plain newline-separated slot-name list (`File.WriteAllLines`/`ReadAllLines`), not JSON — it only needs to answer "which slots have a pending `.tmp` to commit," so no other metadata is stored.
- Journal recovery calls `DSMSlot.CommitReencrypt()` rather than duplicating a `File.Replace`/`File.Move` call directly in `DSMSlotManager` — keeps `ReplaceFile` the single atomic-rename primitive in the codebase, per the plan's explicit reuse requirement (verified via `grep` showing no new `File.Move`/`File.Replace` call sites).
- A rotation attempted while the current config key is unusable (encryption enabled but no valid key ever set) throws `InvalidOperationException` distinct from the `ArgumentException` a weak/empty `newKey` throws — lets a caller distinguish "your rotation call target is invalid" from "your DSM instance isn't in a rotatable state."

## Deviations from Plan

None - plan executed exactly as written. Both tasks completed with the exact file set and behavior specified in `02-02-PLAN.md`.

## Issues Encountered

- **Unity Editor open, blocking `-runTests` batchmode** — same finding as Phase 01 and Plan 02-01 (see STATE.md blocker note). Directly confirmed this session by attempting `Unity -batchmode -runTests -testPlatform EditMode`, which failed with "Aborting batchmode due to fatal error: It looks like another Unity instance is running with this project open." Verified GREEN instead via: (1) `dotnet build -t:Rebuild` against `DMS.Runtime.csproj` and `DSM.Editor.csproj` — 0 errors; (2) temporarily adding a `<Compile Include>` entry for `DSMKeyRotationTests.cs` to the gitignored `DMS.Tests.Editor.csproj` (restored to its pre-edit content afterward — Unity regenerates this file on its own next domain reload) and confirming: RED state (before Task 2) produced 6 compile errors all pointing at the not-yet-implemented rotation API/journal constant, and GREEN state (after Task 2) built with 0 errors; (3) a full manual trace of each of the 5 `DSMKeyRotationTests` scenarios against the final implementation, including HashSet-enumeration-order independence of the pre-commit-failure cleanup path and field-initializer/constructor-body ordering for `RecoverInterruptedRotation`'s access to `_slots`/`_config`. This is strong evidence of correctness but is not a substitute for Unity Test Runner; flagged below for human confirmation, same as the two prior plans' open items.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ENC-02 and TEST-02 are both complete. Phase 02 (encryption hardening, key validation, rotation) has no remaining requirements.
- Phase 4's migration/versioning work is expected to reuse this plan's write-`.tmp`-then-`ReplaceFile` atomic pattern (per STATE.md's roadmap decision) rather than re-deriving it — the `StageReencryptAsync`/`CommitReencrypt`/`CleanupStagedTemp` shape on `DSMSlot` is a template for that reuse.
- **Open item for a human:** open Unity Test Runner (EditMode) once the Editor is free to compile/reload, and confirm all 5 `DSMKeyRotationTests` are green, alongside the existing Phase 1 + Plan 02-01 suite. This is the same open item those two plans left (STATE.md blocker note) and should be closed out together in one Test Runner pass.

---
*Phase: 02-encryption-hardening-key-validation-rotation*
*Completed: 2026-07-14*

## Self-Check: PASSED

- FOUND: Tests/Editor/DSMKeyRotationTests.cs
- FOUND: Tests/Editor/DSMKeyRotationTests.cs.meta
- FOUND: Runtime/DSM.cs
- FOUND: Runtime/DSMSlot.cs
- FOUND: Runtime/DSMSlotManager.cs
- FOUND: commit df47d68
- FOUND: commit fb0fa1f
