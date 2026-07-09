---
phase: 01-foundation-thread-safety-robustness-test-infrastructure
plan: 01
subsystem: testing
tags: [unity, nunit, edittest, thread-safety, lock, concurrency]

# Dependency graph
requires: []
provides:
  - Tests/Editor + Tests/Runtime EditMode test asmdefs (first test infrastructure in this package)
  - DSMTestConfig reflection-based DSMConfig fixture builder
  - DSMSlotConcurrencyTests regression suite (final-state correctness + real disk roundtrip)
  - DSMSlot._dataLock and DSMSlotManager._slotsLock synchronization
affects: [01-02, 01-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Plain lock (object) around short synchronous dictionary critical sections, mirroring DSMWatcher.Notify's lock-snapshot-release shape"
    - "Reflection-based private-field fixture builder (DSMTestConfig.Create) for injecting test config into a ScriptableObject with no public setters"
    - "Final-state-correctness assertions for concurrency tests (assert every key/value after Task.WhenAll, not just 'no exception thrown')"

key-files:
  created:
    - Tests/Editor/DMS.Tests.Editor.asmdef
    - Tests/Runtime/DMS.Tests.Runtime.asmdef
    - Tests/Editor/DSMTestConfig.cs
    - Tests/Editor/DSMTestRunnerSmokeTests.cs
    - Tests/Editor/DSMSlotConcurrencyTests.cs
  modified:
    - Runtime/DSMSlot.cs
    - Runtime/DSMSlotManager.cs

key-decisions:
  - "EditMode-only test assemblies (D-06) — Tests/Runtime asmdef still runs in Edit Mode, it is not PlayMode execution"
  - "Plain lock (D-05 sync half) for _data/_slots; SemaphoreSlim I/O gate deferred to Plan 02"
  - "Save/Load/SaveAsync/LoadAsync were left unlocked in this plan per the plan's explicit scope boundary — async I/O gating is Plan 02's SemaphoreSlim work"

patterns-established:
  - "Test fixture builder pattern: static Create() method + reflection field injection for ScriptableObject configs with private SerializeField backing"
  - "Lock-guarded dictionary CRUD with notify/schedule calls kept outside the lock"

requirements-completed: [TEST-01, CONC-01, CONC-04, TEST-03]

coverage:
  - id: D1
    description: "Unity EditMode Test Runner discovers and runs DSM test assemblies (DMS.Tests.Editor, DMS.Tests.Runtime)"
    requirement: "TEST-01"
    verification:
      - kind: automated_ui
        ref: "Unity -runTests -testFilter DSMTestRunnerSmokeTests -> /tmp/dsm-p01-t1.xml result=Passed (2/2)"
        status: pass
    human_judgment: false
  - id: D2
    description: "100 concurrent Set() calls with distinct keys leave every key present with correct value (final-state correctness)"
    requirement: "CONC-04"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs#ConcurrentSet_WithDistinctKeys_AllKeysPresentWithCorrectValues"
        status: pass
    human_judgment: false
  - id: D3
    description: "Set -> Save -> new-slot Load disk roundtrip returns the written value"
    requirement: "TEST-03"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs#SetSaveLoad_Roundtrip_PersistsValue"
        status: pass
    human_judgment: false
  - id: D4
    description: "DSMSlot._data and DSMSlotManager._slots mutations are lock-guarded against concurrent corruption/drop"
    requirement: "CONC-01"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotConcurrencyTests.cs (full fixture, 5 consecutive runs, zero Failed results)"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-07-09
status: complete
---

# Phase 1 Plan 1: Foundation Test Infrastructure + Slot Synchronization Summary

**Stood up the package's first EditMode NUnit test harness and landed `lock`-guarded synchronization on `DSMSlot._data`/`DSMSlotManager._slots`, turning a data-dropping concurrency bug into a deterministically green regression test.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-09T13:19:00+07:00
- **Completed:** 2026-07-09T13:25:21+07:00
- **Tasks:** 3
- **Files modified:** 7 (5 created, 2 modified)

## Accomplishments
- Created `Tests/Editor/DMS.Tests.Editor.asmdef` and `Tests/Runtime/DMS.Tests.Runtime.asmdef` — both EditMode-only, referencing `DMS.Runtime` (GUID `5c67e4f53d76c46cbabd0a518ee024c0`), UniTask, UniTask.Linq, and Newtonsoft.Json, with `nunit.framework.dll` precompiled and `UNITY_INCLUDE_TESTS` define constraint
- `DSMTestConfig.Create()` fixture builder sets private `DSMConfig` fields (`_autoSave`, `_autoSaveDebounce`, `_encrypt`, `_savePath`) via reflection, defaulting `AutoSave` to false so concurrency tests isolate `_data` locking from the debounce path
- Smoke tests prove the Test Runner discovers `DMS.Tests.Editor` and can reference `DMS.Runtime` types
- `DSMSlotConcurrencyTests.ConcurrentSet_WithDistinctKeys_AllKeysPresentWithCorrectValues` — 100 parallel `Task.Run(() => slot.Set(...))` calls, asserting final-state correctness per key (not just "no exception"). Confirmed RED against the original unsynchronized `DSMSlot` (`result="Failed"` before the fix), then GREEN after Task 3's lock (5 consecutive runs, zero failures — deterministic, not flaky)
- `DSMSlotConcurrencyTests.SetSaveLoad_Roundtrip_PersistsValue` — real `Set → Save → new-slot Load` disk roundtrip, green from the start (proves the persistence path independent of the lock fix)
- `DSMSlot._dataLock` added, guarding `Set`/`Get`/`Has`/`Delete`/`Clear`/`SeedDefaults` dictionary access; `_watcher.Notify()`/`ScheduleSave()` remain outside the lock (mirrors `DSMWatcher.Notify`'s snapshot-then-release shape)
- `DSMSlotManager._slotsLock` added, guarding `GetOrCreateSlot`'s TryGetValue/create/assign and `DeleteSlot`'s `_slots.Remove` — file-delete I/O in `DeleteSlot` stays outside the lock
- No `SemaphoreSlim` introduced (correctly deferred to Plan 02 per plan scope)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create EditMode test assemblies + config fixture + smoke test** - `92e7997` (feat)
2. **Task 2: Write failing concurrency regression test + real Save/Load roundtrip** - `9859a97` (test — RED)
3. **Task 3: Synchronize DSMSlot._data and DSMSlotManager._slots** - `7abc4ae` (feat — GREEN)

_TDD gate sequence verified: `test(...)` commit `9859a97` precedes `feat(...)` commit `7abc4ae`. No separate refactor commit was needed._

## Files Created/Modified
- `Tests/Editor/DMS.Tests.Editor.asmdef` - EditMode test assembly referencing DMS.Runtime + UniTask + Newtonsoft.Json
- `Tests/Runtime/DMS.Tests.Runtime.asmdef` - Second EditMode test assembly per D-06 (name only, still Edit Mode)
- `Tests/Editor/DSMTestConfig.cs` - Static `DSMTestConfig.Create()` fixture builder, reflection-sets private `DSMConfig` fields
- `Tests/Editor/DSMTestRunnerSmokeTests.cs` - `TestRunner_IsWired_Passes` + `DSMTestConfig_Create_AppliesAutoSaveFalse` smoke fixture
- `Tests/Editor/DSMSlotConcurrencyTests.cs` - `ConcurrentSet_WithDistinctKeys_AllKeysPresentWithCorrectValues` (final-state correctness) + `SetSaveLoad_Roundtrip_PersistsValue` (real disk roundtrip)
- `Runtime/DSMSlot.cs` - Added `_dataLock`; wrapped `Set`/`Get`/`Has`/`Delete`/`Clear`/`SeedDefaults` dictionary access in `lock (_dataLock)`
- `Runtime/DSMSlotManager.cs` - Added `_slotsLock`; wrapped `GetOrCreateSlot`'s dictionary read/write and `DeleteSlot`'s `_slots.Remove` in `lock (_slotsLock)`

## Decisions Made
None beyond what was already pinned in `01-CONTEXT.md`/`01-SKELETON.md` (D-04, D-05, D-06) — plan executed exactly as specified. `Save`/`Load`/`SaveAsync`/`LoadAsync` were intentionally left without locking in this plan; that is Plan 02's `SemaphoreSlim` I/O-gate work, not a gap in this plan.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None. The concurrency test's RED baseline was confirmed with a dedicated pre-fix run (`Unity -testFilter DSMSlotConcurrencyTests.ConcurrentSet_...` returned `result="Failed"` against the unmodified `DSMSlot`), and GREEN was confirmed deterministically with 5 consecutive full-fixture runs after the lock was added (zero flakes).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02 (SemaphoreSlim I/O gate, atomic temp-write-then-rename, debounce/CTS race rewrite) can build directly on the `_dataLock`/`_slotsLock` primitives landed here — no rework needed.
- `DSMTestConfig` fixture builder and the `Tests/Editor`/`Tests/Runtime` asmdefs are ready for reuse by Plan 02 and Plan 03's test suites.
- No blockers.

---
*Phase: 01-foundation-thread-safety-robustness-test-infrastructure*
*Completed: 2026-07-09*

## Self-Check: PASSED

All created/modified files verified present on disk; all task commits (`92e7997`, `9859a97`, `7abc4ae`) and the summary commit (`a009a6a`) verified present in git log.
