---
phase: 01-foundation-thread-safety-robustness-test-infrastructure
plan: 03
subsystem: testing
tags: [unity, nunit, validation, json, robustness, dsm]

# Dependency graph
requires:
  - phase: 01-foundation-thread-safety-robustness-test-infrastructure (plan 01-02)
    provides: DSMSlot._data/_slots locking, SemaphoreSlim I/O gate, atomic temp-write-then-rename save, rewritten debounce loop
provides:
  - DSMSlotNameValidator static utility rejecting path-traversal/separator/reserved/out-of-charset slot names
  - Validated DSMSlotManager boundary (GetOrCreateSlot, DeleteSlot)
  - Malformed-JSON load fallback in DSMSlot.Load/LoadAsync (warn + SeedDefaults, never throw)
  - Widget-missing-IDSMWidget-component warning in DSMRuntimePanel.BuildWidgets
  - DSMSlotNameValidatorTests, DSMSlotLoadRobustnessTests
affects: [phase-02-encryption-hardening, phase-04-migration, any-future-phase-touching-dsmslot-or-dsmslotmanager]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Throw-at-the-boundary input validation (ArgumentException) applied to DSMSlotNameValidator, matching existing DSMRuntimePanel InvalidOperationException convention"
    - "Silent-fallback-with-warning (Debug.LogWarning + SeedDefaults) reused verbatim for malformed-JSON load, matching the existing SeedDefaults per-field warning convention"

key-files:
  created:
    - Runtime/DSMSlotNameValidator.cs
    - Tests/Editor/DSMSlotNameValidatorTests.cs
    - Tests/Editor/DSMSlotLoadRobustnessTests.cs
  modified:
    - Runtime/DSMSlotManager.cs
    - Runtime/DSMSlot.cs
    - Runtime/DSMRuntimePanel.cs

key-decisions:
  - "Slot-name validation rule set matches D-03/CONCERNS.md verbatim: alphanumeric + _/- only, explicit '..' reject, case-insensitive Windows-reserved-device-name reject"
  - "Malformed-JSON fallback wraps only the _serializer.Deserialize(json) call (not the file read/decrypt), per D-02, so decrypt/IO errors still surface distinctly from parse errors"

patterns-established:
  - "New runtime utilities follow the DSM{Noun} static-class naming convention (DSMSlotNameValidator alongside DSMPaths/DSMSerializer/DSMEncryptor)"

requirements-completed: [BUGS-03, BUGS-04, TEST-04, CONC-04]
# TEST-03 is NOT included here — it is satisfied by the full-suite green
# confirmation at the Task 3 checkpoint, not yet human-confirmed as of this
# draft. Re-check this list when finalizing after the checkpoint resolves.

coverage:
  - id: D1
    description: "DSMSlotNameValidator.Validate rejects path-traversal, path-separator, Windows-reserved-device, and out-of-charset slot names with ArgumentException; accepts normal names"
    requirement: "BUGS-03"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotNameValidatorTests.cs#Validate_WithInvalidName_ThrowsArgumentException, Validate_WithValidName_DoesNotThrow"
        status: unknown
    human_judgment: true
    rationale: "Unity Editor was open on this project for the whole session, so -runTests -batchmode could not run (second-instance conflict, expected per known environment constraint). Verified by dotnet build (0 errors against real Unity/UniTask/DMS.Runtime references) and manual trace of each assertion against the behavior spec, but actual NUnit pass/fail was not observed by the executor. Human confirms via Test Runner at the Task 3 checkpoint."
  - id: D2
    description: "DSMSlotNameValidator wired at the top of DSMSlotManager.GetOrCreateSlot and DeleteSlot, rejecting invalid names before any dictionary/file-path work"
    requirement: "BUGS-03"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotNameValidatorTests.cs#GetOrCreateSlot_WithInvalidName_ThrowsBeforeAnyFileAccess, DeleteSlot_WithInvalidName_ThrowsBeforeAnyFileAccess"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Editor batchmode conflict as D1 — human confirms via Test Runner at the Task 3 checkpoint."
  - id: D3
    description: "Malformed/corrupt JSON on Load/LoadAsync does not throw — logs a warning naming the slot and parse error, falls back to SeedDefaults()"
    requirement: "BUGS-03"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotLoadRobustnessTests.cs#Load_WithMalformedJsonFile_FallsBackToDefaultsWithoutThrowing, LoadAsync_WithMalformedJsonFile_FallsBackToDefaults, Load_WithMalformedJsonFile_LogsWarningNamingSlotAndError, Load_WithMalformedJsonFile_SeedsDSMConstantDefaults"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Editor batchmode conflict — human confirms via Test Runner at the Task 3 checkpoint."
  - id: D4
    description: "DSMRuntimePanel.BuildWidgets logs a clear Debug.LogError and skips (continue) a widget prefab missing its IDSMWidget component instead of silently no-op'ing"
    requirement: "BUGS-04"
    verification:
      - kind: manual_procedural
        ref: "Task 3 checkpoint step 5: scratch scene with a component-less widget prefab in Play Mode, confirm Console error and skip"
        status: unknown
    human_judgment: true
    rationale: "Requires a scene/prefab in the Unity Editor and Play Mode observation — not automatable from this executor. Optional manual spot-check at the Task 3 checkpoint."
  - id: D5
    description: "Full Phase 1 Foundation EditMode suite (concurrency, atomic-save, debounce, validation, robustness) is green in the Unity Test Runner"
    requirement: "TEST-03"
    verification: []
    human_judgment: true
    rationale: "This is the Task 3 blocking human-verify checkpoint itself — by design requires the user to run the Unity Test Runner and confirm 0 failures. Not yet confirmed as of this draft."

# Metrics
duration: 12min (Tasks 1-2 only; Task 3 checkpoint pending)
completed: PENDING — plan paused at Task 3 blocking checkpoint
status: paused-checkpoint
---

# Phase 1 Plan 3: Slot-Name Validation, Malformed-JSON Fallback & Widget Warning Summary

**DSMSlotNameValidator rejecting path-traversal/reserved slot names, malformed-JSON load fallback via SeedDefaults, and a widget-missing-component warning — Tasks 1-2 complete, Task 3 (full-suite human verification) pending**

## Performance

- **Duration:** ~12 min (Tasks 1-2)
- **Started:** 2026-07-13T07:16:00Z (approx, continuing directly after 01-02 completion)
- **Completed:** Not yet — paused at Task 3 checkpoint
- **Tasks:** 2 of 3 complete (Task 3 is a blocking human-verify checkpoint)
- **Files modified:** 7 (3 created, 4 modified)

## Accomplishments
- `DSMSlotNameValidator` created and wired into `DSMSlotManager.GetOrCreateSlot`/`DeleteSlot` — path-traversal, path-separator, and Windows-reserved-device slot names are rejected with `ArgumentException` before any dictionary or file-path work (BUGS-03/D-03)
- Malformed/corrupt JSON on `DSMSlot.Load()`/`LoadAsync()` no longer throws — it logs a warning naming the slot and the parse error, then falls back to `SeedDefaults()` (D-02, matching the existing per-field warning convention)
- `DSMRuntimePanel.BuildWidgets` now explicitly checks for a missing `IDSMWidget` component, logs a clear `Debug.LogError(..., this)`, and `continue`s past it instead of silently no-op'ing via the null-conditional `Setup()` call (BUGS-04)

## Task Commits

Each task was committed atomically:

1. **Task 1: DSMSlotNameValidator + wire into DSMSlotManager boundary + validator tests** - `4390186` (feat)
2. **Task 2: Malformed-JSON load fallback + widget-missing-component warning + robustness tests** - `19e6e7d` (feat)
3. **Task 3: Full-suite human-verify checkpoint** - NOT YET REACHED FOR COMPLETION (plan paused here)

**Plan metadata:** pending — will be added when Task 3 resolves and the plan is finalized.

## Files Created/Modified
- `Runtime/DSMSlotNameValidator.cs` - New static validator: regex charset check, `..` reject, case-insensitive Windows-reserved-device-name reject
- `Runtime/DSMSlotManager.cs` - `DSMSlotNameValidator.Validate(name)` called as the first statement of `GetOrCreateSlot` and `DeleteSlot`
- `Runtime/DSMSlot.cs` - `try/catch` around `_serializer.Deserialize(json)` in both `Load()` and `LoadAsync()`; catch logs a warning and calls `SeedDefaults()`
- `Runtime/DSMRuntimePanel.cs` - Widget instantiation fetches `IDSMWidget` into a local, null-checks it, logs an error and `continue`s on failure
- `Tests/Editor/DSMSlotNameValidatorTests.cs` - Throw/no-throw cases for `Validate`, plus manager-boundary enforcement tests
- `Tests/Editor/DSMSlotLoadRobustnessTests.cs` - Sync/async no-throw fallback, warning-message assertion, DSMConstant-seeded-defaults case

## Decisions Made
- Validation rule set matches D-03/CONCERNS.md exactly: `^[A-Za-z0-9_-]+$` anchored regex, explicit `..` substring reject, case-insensitive reserved-device-name list (CON, PRN, AUX, NUL, COM1-9, LPT1-9) — no more-permissive fallback introduced
- Malformed-JSON `try/catch` wraps only the `_serializer.Deserialize(json)` call, not the preceding file-read/decrypt step, so decryption/IO errors (a different failure class) still propagate distinctly from JSON-parse errors, per D-02's scope

## Deviations from Plan

None — plan executed exactly as written for Tasks 1 and 2. One environment-driven adaptation (not a plan deviation, a pre-declared constraint): the Unity Editor was open on the project for the whole session, so the plan's `-runTests -batchmode` automated verification blocks could not run (expected "another Unity instance is running" conflict). Substituted `dotnet build DMS.Tests.Editor.csproj` (0 errors both times) plus manual trace of every new test's assertions against the task's `<behavior>` spec, per the known environment constraint given for this execution. Actual NUnit pass/fail for the new `DSMSlotNameValidatorTests` and `DSMSlotLoadRobustnessTests` suites is therefore **not yet confirmed** — this is deferred to the human at the Task 3 checkpoint alongside the full-suite green confirmation, which was already the plan's own design.

Note: the generated `DMS.Runtime.csproj` and `DMS.Tests.Editor.csproj` (gitignored, Unity-generated) were manually patched with `<Compile Include>` entries for the three new files to make local `dotnet build` verification possible before Unity's own project-file regeneration picks them up. This is a local build-verification aid only — no committed file was affected.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness

**BLOCKED on Task 3 checkpoint.** Once the user confirms the full Phase 1 Foundation EditMode suite is green in the Unity Test Runner (and optionally spot-checks the BUGS-04 widget warning in Play Mode), Phase 1 (foundation-thread-safety-robustness-test-infrastructure) is complete and Phase 2 can begin. This SUMMARY should be updated (status → `complete`, coverage statuses → `pass`, duration/completed timestamps finalized, Task 3 commit/plan-metadata commit added) once that confirmation is received.

## Self-Check: PASSED (Tasks 1-2 only)
- FOUND: Runtime/DSMSlotNameValidator.cs
- FOUND: Runtime/DSMSlotNameValidator.cs.meta
- FOUND: Tests/Editor/DSMSlotNameValidatorTests.cs
- FOUND: Tests/Editor/DSMSlotNameValidatorTests.cs.meta
- FOUND: Tests/Editor/DSMSlotLoadRobustnessTests.cs
- FOUND: Tests/Editor/DSMSlotLoadRobustnessTests.cs.meta
- FOUND commit: 4390186 (Task 1)
- FOUND commit: 19e6e7d (Task 2)

Task 3 (checkpoint) self-check deferred until the checkpoint resolves.

---
*Phase: 01-foundation-thread-safety-robustness-test-infrastructure*
*Completed: PENDING (paused at Task 3 checkpoint)*
