---
phase: 01-foundation-thread-safety-robustness-test-infrastructure
plan: 02
subsystem: testing
tags: [unity, unitask, semaphoreslim, debounce, concurrency, nunit]

# Dependency graph
requires:
  - phase: 01-foundation-thread-safety-robustness-test-infrastructure (plan 01)
    provides: "_dataLock synchronization of DSMSlot._data and DSMSlotManager._slots, EditMode test infrastructure (asmdefs, DSMTestConfig fixture builder)"
provides:
  - "SemaphoreSlim _ioGate serializing Save/SaveAsync/Load/LoadAsync (released in finally)"
  - "Temp-write-then-atomic-rename save path (File.Replace/File.Move) for both sync and async saves"
  - "Single long-lived debounce loop replacing per-Set() CancellationTokenSource churn"
  - "Concurrency-matrix regression tests: Set+Load, Set+SaveAsync, multi-watcher"
affects: [phase-2-encryption-key-rotation, phase-4-migration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Async-spanning I/O gate: SemaphoreSlim(1,1) with WaitAsync()/Wait() + try/finally Release()"
    - "Single long-lived debounce loop with a monotonic request-version counter under a dedicated lock, replacing per-call CancellationTokenSource cancel/dispose"
    - "Temp-file-then-atomic-rename publish (File.Replace when destination exists, File.Move otherwise) for crash-safe writes"

key-files:
  created: []
  modified:
    - Runtime/DSMSlot.cs
    - Tests/Editor/DSMSlotDebounceTests.cs
    - Tests/Editor/DSMSlotConcurrencyTests.cs

key-decisions:
  - "Debounce rewrite drops CancellationTokenSource entirely (no lifetime CTS retained) since DSMSlot has no teardown/dispose method that would need one; ScheduleSave()/CancelDebounce() signatures are unchanged so Set() at line ~44 needed zero edits"
  - "CancelDebounce() is now an intentional no-op (documented in a code comment) — there is nothing to cancel/dispose in the new design; an explicit Save()/SaveAsync() simply proceeds and any still-pending debounce loop iteration observes up-to-date state on its next wake"
  - "Automated Unity EditMode batchmode test execution could not be run this session — the user's own Unity Editor already had the project open, and Unity refuses a second batchmode instance on the same project. Correctness was instead verified via `dotnet build` (0 errors across UniTask, DMS.Runtime, DMS.Tests.Editor) plus manual trace of the debounce loop and concurrency tests against their documented behavior. This is a genuine verification gap — see 'Verification Status' below."

requirements-completed: [CONC-01, CONC-02, CONC-03, CONC-04, TEST-03]

coverage:
  - id: D1
    description: "Temp-write-then-atomic-rename save path (Save/SaveAsync) with SemaphoreSlim I/O gate released in finally — carried over from Task 1, already committed prior to this session (fd0146e)"
    requirement: "CONC-01"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotAtomicSaveTests.cs#SaveAsync_WritesCompleteFile_NoTempLeftOver"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSlotAtomicSaveTests.cs#Save_IsAtomic_DestinationNeverPartial"
        status: unknown
    human_judgment: true
    rationale: "Unity batchmode test run was blocked this session by the user's own open Editor instance (project lock). Task 1 was implemented and committed in a prior session; this session did not re-verify it via the test runner. A human must run EditMode tests (Test Runner window or a closed-Editor batchmode run) to confirm."
  - id: D2
    description: "ScheduleSave debounce rewritten as a single long-lived loop (DebouncedSaveLoopAsync) with a monotonic request-version counter, eliminating per-Set() CancellationTokenSource creation/disposal"
    requirement: "CONC-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotDebounceTests.cs#RapidSetStorm_NeverThrowsDisposedCts"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSlotDebounceTests.cs#RapidSetStorm_PersistsLastValue"
        status: unknown
    human_judgment: true
    rationale: "Code compiles cleanly (dotnet build, 0 errors) and the loop logic was manually traced against both test scenarios, but the Unity Test Runner could not execute this session due to the project's Editor lock. A human must run these two tests to confirm green before treating CONC-02 as fully closed."
  - id: D3
    description: "Concurrency-matrix regression tests added: ConcurrentSetAndLoad_NeverCorruptsData, ConcurrentSetAndSaveAsync_FinalFileParses, MultiWatcher_AllSubscribersReceiveValue"
    requirement: "CONC-04, TEST-03"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs#ConcurrentSetAndLoad_NeverCorruptsData"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs#ConcurrentSetAndSaveAsync_FinalFileParses"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs#MultiWatcher_AllSubscribersReceiveValue"
        status: unknown
    human_judgment: true
    rationale: "Same Editor-lock blocker as D1/D2 — tests compile (dotnet build DMS.Tests.Editor.csproj succeeded, 0 errors) and were designed per the plan's final-state-correctness guidance (PITFALLS.md Pitfall 6), but were never executed through the Unity Test Runner this session."

duration: ~20min (this session, Tasks 2-3 only; Task 1 was completed in a prior session)
completed: 2026-07-13
status: complete
---

# Phase 1 Plan 2: Debounce Rewrite + Concurrency-Matrix Tests Summary

**Single long-lived debounce loop replaces per-Set() CancellationTokenSource churn in DSMSlot, plus three new concurrency-matrix regression tests (Set+Load, Set+SaveAsync, multi-watcher) closing out TEST-03**

## Performance

- **Duration:** ~20 min (this session — resumed mid-plan; Task 1 was already committed as `fd0146e` in a prior session)
- **Tasks:** 3 (Task 1 verified already-complete from a prior session; Tasks 2-3 executed this session)
- **Files modified:** 3 (`Runtime/DSMSlot.cs`, `Tests/Editor/DSMSlotDebounceTests.cs`, `Tests/Editor/DSMSlotConcurrencyTests.cs`)

## Accomplishments
- Verified Task 1 (SemaphoreSlim `_ioGate` + atomic temp-write-then-rename) was already correctly implemented and committed (`fd0146e`) — did not redo this work
- Rewrote `DSMSlot.ScheduleSave()`/`DebouncedSaveAsync()`/`CancelDebounce()` as a single long-lived `DebouncedSaveLoopAsync()` loop tracking a monotonic `_debounceRequestVersion` counter under a dedicated `_debounceLock` — the `ObjectDisposedException` disposal race no longer exists because there is no per-`Set()` `CancellationTokenSource` to dispose
- Extended `Tests/Editor/DSMSlotConcurrencyTests.cs` with three new final-state-correctness tests covering the CONC-04/TEST-03 matrix: `Set`+`Load`, `Set`+`SaveAsync`, and multi-subscriber `WatchAsync<T>`

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SemaphoreSlim I/O gate + atomic temp-write-then-rename** - `fd0146e` (feat) — completed in a prior session, verified not redone
2. **Task 2: Rewrite ScheduleSave debounce as a single long-lived loop** - `45a7966` (feat)
3. **Task 3: Concurrency-matrix regression tests** - `4ecab90` (test)

**Plan metadata:** (this commit, docs)

## Files Created/Modified
- `Runtime/DSMSlot.cs` - Removed `_debounceCts` field and the CTS-cancel-and-recreate pattern; added `_debounceLock`/`_debounceRequestVersion`/`_debounceLoopRunning` fields and `DebouncedSaveLoopAsync()`; `CancelDebounce()` is now a documented no-op
- `Tests/Editor/DSMSlotDebounceTests.cs` - `RapidSetStorm_NeverThrowsDisposedCts`, `RapidSetStorm_PersistsLastValue` (pre-existing on disk from a prior session, verified against the plan's `<behavior>` spec, left unmodified)
- `Tests/Editor/DSMSlotConcurrencyTests.cs` - Added `ConcurrentSetAndLoad_NeverCorruptsData`, `ConcurrentSetAndSaveAsync_FinalFileParses`, `MultiWatcher_AllSubscribersReceiveValue`

## Decisions Made
- No lifetime `CancellationTokenSource` was retained for slot teardown — `DSMSlot` has no `Dispose`/teardown method today, so there is nothing for a lifetime CTS to be cancelled by; if teardown is added in a future phase, a lifetime CTS can be introduced then without touching this debounce design
- `CancelDebounce()` kept as a stable no-op method (rather than removing it) to preserve the existing call sites in `Save()`/`SaveAsync()` verbatim, per the plan's instruction to keep `ScheduleSave()`'s signature/call shape stable
- Multi-watcher test uses `UniTask.WhenAll` + a 5-second `CancellationTokenSource` timeout rather than a fixed sleep, so a real regression in `DSMWatcher`'s concurrent-notify path fails fast instead of hanging the suite

## Deviations from Plan

None — plan executed exactly as written for Tasks 2 and 3. Task 1 was already complete from a prior session and was verified, not redone, per the resume instructions.

## Issues Encountered

**Unity Editor already open on the project, blocking automated batchmode test execution.** The user's own Unity Editor (PID 37499) had `/Users/draftsama/Works/Unity/counter-stack-game` open for the entire session. Every `-runTests -batchmode` invocation (attempted 3 times, including a final attempt after all commits) failed fast with `"It looks like another Unity instance is running with this project open."` Unity does not support a second instance opening the same project, and force-quitting the user's live Editor session risked discarding unsaved work outside this plan's scope — so it was not attempted.

As a substitute verification step, `dotnet build DMS.Tests.Editor.csproj` was run twice (after Task 2 and again after Task 3) against the Unity-generated `.csproj` files, referencing the real `UniTask`/`DMS.Runtime` project references — both builds succeeded with **0 errors** (1 pre-existing analyzer warning unrelated to this plan, in `UniTask`'s own `MonoBehaviourMessagesTriggers.cs`). This confirms the C# compiles correctly against the project's actual assembly references, but does **not** confirm runtime test-pass status, since NUnit test execution requires the Unity Test Framework's runner (and some fixtures, e.g. `DSMTestConfig.Create()`, call `ScriptableObject.CreateInstance`, which requires the native Unity engine to be initialized — not just the managed DLL).

**Action needed:** A human (or a future agent with exclusive access to the project) should run the EditMode Test Runner — either via the Unity Editor's `Window > General > Test Runner` window (works even with the Editor already open, since it doesn't require a second instance) or via `-runTests -batchmode` once the Editor is closed — filtered to `DSMSlotDebounceTests` and `DSMSlotConcurrencyTests`, and confirm zero Failed before treating this plan's acceptance criteria as fully verified.

## Verification Status

| Check | Status | Evidence |
|---|---|---|
| Code compiles (`dotnet build DMS.Tests.Editor.csproj`) | PASS | 0 errors, 1 pre-existing unrelated warning |
| `grep -c "new CancellationTokenSource" Runtime/DSMSlot.cs` (must be ≤1) | PASS | 0 occurrences |
| `_ioGate`/`.tmp`/`File.Move` atomic-save markers present | PASS | Confirmed via grep (Task 1, carried over) |
| `DSMSlotDebounceTests` — actual Unity Test Runner pass | **NOT CONFIRMED** | Blocked by Editor lock; logic manually traced against both test scenarios |
| `DSMSlotConcurrencyTests` (full matrix) — actual Unity Test Runner pass | **NOT CONFIRMED** | Blocked by Editor lock; logic manually traced against final-state assertions |

## User Setup Required

None — no external service configuration required. **Recommended follow-up:** run the EditMode Test Runner (Test Runner window works fine with the Editor already open) for `DSMSlotDebounceTests` and `DSMSlotConcurrencyTests` before starting Phase 2, to close the verification gap noted above.

## Next Phase Readiness

- Phase 2 (encryption key rotation) can reuse the temp-write-then-atomic-rename primitive established in Task 1 as-is
- Phase 4 (migration) can likewise reuse the same atomic-write pattern
- Outstanding: confirm the three new/changed test files actually pass in the Unity Test Runner (see Verification Status above) — this is the only open item blocking full confidence in this plan's acceptance criteria

---
*Phase: 01-foundation-thread-safety-robustness-test-infrastructure*
*Completed: 2026-07-13*

## Self-Check: PASSED

- FOUND: Runtime/DSMSlot.cs
- FOUND: Tests/Editor/DSMSlotDebounceTests.cs
- FOUND: Tests/Editor/DSMSlotConcurrencyTests.cs
- FOUND: .planning/phases/01-foundation-thread-safety-robustness-test-infrastructure/01-02-SUMMARY.md
- FOUND commit: fd0146e (Task 1, prior session)
- FOUND commit: 45a7966 (Task 2)
- FOUND commit: 4ecab90 (Task 3)
