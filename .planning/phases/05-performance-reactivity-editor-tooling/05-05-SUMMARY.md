---
phase: 05-performance-reactivity-editor-tooling
plan: 05
subsystem: testing
tags: [editmode, nunit, editor-state, regression, assembly-references]

requires:
  - phase: 05-performance-reactivity-editor-tooling
    provides: DSMManagerSlotOps state model (05-03) and DSMSlotVersionPanel (05-04)
provides:
  - EditMode coverage of slot switch and delete-while-selected transitions
  - Deterministic post-delete fallback to the configured default slot
  - DMS.Tests.Editor → DSM.Editor assembly reference + internals access
affects: [future-editor-work]

tech-stack:
  added: []
  patterns:
    - "Editor UI state is tested headlessly through the extracted ops class — never by rendering IMGUI"
    - "Optional injected parameter lets a panel avoid touching the DSM global facade under test"

key-files:
  created:
    - Tests/Editor/DSMManagerWindowStateTests.cs
    - Editor/AssemblyInfo.cs
  modified:
    - Editor/DSMManagerSlotOps.cs
    - Editor/DSMSlotVersionPanel.cs
    - Tests/Editor/DMS.Tests.Editor.asmdef

key-decisions:
  - "No-stale-carry-over is asserted on SlotData and ActiveSlot, not Defaults: the defaults list intentionally accumulates discovered runtime keys because it models DSMConstant, not the selected slot"
  - "Post-delete selection now lands on the configured default slot instead of the alphabetically first remaining slot"
  - "DSMSlotVersionPanel.Refresh takes an optional currentVersion so tests never build the global DSM manager"
  - "Verified by compiling DMS.Tests.Editor with dotnet — all 16 test sources, including this phase's four new fixtures, type-check"

patterns-established:
  - "Tests construct DSMManagerSlotOps the same way the window does (BindConfig → ResetDefaults → DiscoverSlots)"

requirements-completed: [TEST-05]

coverage:
  - id: D1
    description: "Switching the selected slot loads the new slot's data with no carry-over from the previous slot, and switching back restores the original"
    requirement: TEST-05
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMManagerWindowStateTests.cs#SwitchSelectedSlot_LoadsNewSlotData_NoStaleCarryOver, SwitchBackToPreviousSlot_RestoresItsData"
        status: unknown
    human_judgment: true
    rationale: "Project rule forbids running Unity tests from the agent; the EditMode run is the criterion-5 acceptance gate"
  - id: D2
    description: "Deleting the selected slot falls back deterministically to the default slot, leaves no stale data or error, and keeps the remaining slots selectable"
    requirement: TEST-05
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMManagerWindowStateTests.cs#DeleteSelectedSlot_FallsBackAndClearsStaleState, DeleteSelectedSlot_LeavesOtherSlotsSelectable"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner gate — cannot be executed by the agent"
  - id: D3
    description: "The version panel drops the deleted slot's row after a refresh"
    requirement: TEST-05
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMManagerWindowStateTests.cs#DeleteSelectedSlot_VersionPanelNoLongerShowsDeletedSlot"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner gate — cannot be executed by the agent"
  - id: D4
    description: "The tests compile against the Editor assembly's state model without rendering IMGUI"
    verification:
      - kind: other
        ref: "dotnet build DMS.Tests.Editor.csproj -clp:ErrorsOnly → 0 errors; grep: no OnGUI/ShowWindow/GetWindow, no Assert.ThrowsAsync"
        status: pass
    human_judgment: false

duration: 19 min
completed: 2026-07-22
status: complete
---

# Phase 5 Plan 05: Editor Window State Transition Tests Summary

**Five headless EditMode cases drive `DSMManagerSlotOps` directly to prove that switching slots carries no stale data and that deleting the selected slot lands deterministically on the default slot with the version panel's row gone.**

## Performance

- **Duration:** 19 min
- **Started:** 2026-07-22T15:14:00Z
- **Completed:** 2026-07-22T15:33:00Z
- **Tasks:** 2
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments
- `DSMManagerWindowStateTests` covers switch-with-no-carry-over, switch-back, delete-selected fallback, remaining-slots-still-selectable, and version-panel-drops-deleted-slot — all by calling `SelectSlot`/`DeleteActiveSlot` on the real state model, with no `OnGUI`, `GetWindow`, or `ShowWindow` anywhere.
- Deleting the selected slot now lands on the configured default slot (which `DiscoverSlots` always re-adds) rather than whichever name sorted first — a deterministic, documented fallback.
- `DMS.Tests.Editor` gained a reference to `DSM.Editor`, and `Editor/AssemblyInfo.cs` grants it `InternalsVisibleTo`, making the internal Editor state model reachable from tests.
- `DSMSlotVersionPanel` exposes `InspectedSlots` and accepts an optional `currentVersion` on `Refresh`, so a test can assert on its rows without building the global `DSM` manager against the project's real save directory.
- Verified by compiling the whole test assembly: `dotnet build DMS.Tests.Editor.csproj` → **0 errors** across all 16 sources, including this phase's four new fixtures.

## Task Commits

1. **Task 2: testability seams (assembly refs, panel view, delete fallback)** — `369897a` (refactor)
2. **Task 1: window state transition tests** — `08dde18` (test)

_Committed seams-first so both commits build; the plan lists Task 1 first, but Task 1's fixture does not compile without Task 2's assembly reference._

## Files Created/Modified
- `Tests/Editor/DSMManagerWindowStateTests.cs` — 5-case headless state fixture
- `Editor/AssemblyInfo.cs` — `InternalsVisibleTo("DMS.Tests.Editor")` for the Editor assembly
- `Tests/Editor/DMS.Tests.Editor.asmdef` — added the `DSM.Editor` reference
- `Editor/DSMManagerSlotOps.cs` — post-delete fallback selects the configured default slot
- `Editor/DSMSlotVersionPanel.cs` — `InspectedSlots` view + optional `currentVersion` argument

## Decisions Made
- **Staleness is asserted on `SlotData`/`ActiveSlot`, not `Defaults`.** `SyncRuntimeKeys` deliberately promotes any key it discovers into the defaults list, because that list models `DSMConstant` (what "Save DSMConstant.cs" writes), not the selected slot. Keys accumulating there across slot switches is the designed behavior; the loaded slot data is what must not carry over, and it is rebuilt from scratch on every `LoadSlotData`.
- **Fallback changed to the default slot.** The 05-03 extraction preserved the original `_availableSlots[0]` behavior. Plan 05-05 asks for an explicit deterministic rule, and the configured default slot is both more predictable for a user and always present in the rediscovered list.
- **`currentVersion` is injected rather than always read from `DSM`.** The parameter defaults to `DSM.CurrentSaveVersion`, so the window call site is unchanged, but the test supplies a literal and never touches the global facade.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The test assembly could not see the Editor assembly at all**
- **Found during:** Task 1 (authoring the fixture)
- **Issue:** `DMS.Tests.Editor.asmdef` referenced only the Runtime assembly, and `DSMManagerSlotOps`/`DSMSlotVersionPanel` are `internal` to `DSM.Editor`. The fixture the plan describes could not compile as written — the plan assumed the seam was reachable.
- **Fix:** Added the `DSM.Editor` GUID to the test asmdef's references and created `Editor/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("DMS.Tests.Editor")]`, keeping the Editor types internal rather than making them public.
- **Files modified:** Tests/Editor/DMS.Tests.Editor.asmdef, Editor/AssemblyInfo.cs
- **Verification:** `dotnet build DMS.Tests.Editor.csproj -clp:ErrorsOnly` → 0 errors, with the new fixture compiled.
- **Committed in:** `369897a`

**2. [Rule 2 - Missing Critical] The version panel had no observable readings**
- **Found during:** Task 1 (version-panel assertion)
- **Issue:** The plan's third test asserts the panel's readings no longer include the deleted slot, but `_readings` is private and `Refresh` reads `DSM.CurrentSaveVersion`, which would construct the global manager against the project's real save directory during a test.
- **Fix:** Added an `internal IEnumerable<string> InspectedSlots` view and an optional `int? currentVersion` parameter on `Refresh` (defaulting to the existing `DSM.CurrentSaveVersion` behavior, so the window call site is untouched).
- **Files modified:** Editor/DSMSlotVersionPanel.cs
- **Verification:** the panel test compiles and asserts row membership; `dotnet build DSM.Editor.csproj` → 0 errors.
- **Committed in:** `369897a`

**3. [Rule 1 - Behavior] Post-delete fallback made explicit**
- **Found during:** Task 2 (seam review)
- **Issue:** `DeleteActiveSlot` selected `_availableSlots[0]` — deterministic but arbitrary, and the plan asks for an explicit documented rule.
- **Fix:** Falls back to the configured default slot, which `DiscoverSlots` always re-adds. This is an intentional, plan-sanctioned change to the window's post-delete selection.
- **Files modified:** Editor/DSMManagerSlotOps.cs
- **Verification:** `DeleteSelectedSlot_FallsBackAndClearsStaleState` asserts `ActiveSlot == config.DefaultSlot`; Editor assembly compiles.
- **Committed in:** `369897a`

---

**Total deviations:** 3 auto-fixed (1 blocking, 1 missing critical, 1 behavior)
**Impact on plan:** All three were required to make the planned tests compile and assert what the plan specifies. The only user-visible change is the post-delete landing slot, which the plan explicitly authorises.

## Issues Encountered
- The generated `.csproj` files list sources explicitly, so the four test fixtures added across this phase plus the new Editor sources had to be registered before the compile check covered them. Those files are git-ignored and Unity regenerates them on import — but note that Unity must reimport before its own compilation picks the changes up.

## User Setup Required
None - no external service configuration required.

## Verification Status

- **Automated (no Unity):** `dotnet build DMS.Tests.Editor.csproj -clp:ErrorsOnly` → Build succeeded, 0 errors across 16 test sources (the strongest check available without Unity: it proves the asmdef reference, the internals grants, `slot.FlushWatchers()`, and every new fixture type-check). `dotnet build DSM.Editor.csproj` and `dotnet build DMS.Runtime.csproj` → 0 errors. Greps confirm 5 `[Test]` methods, no `OnGUI`/`ShowWindow`/`GetWindow`, and no `Assert.ThrowsAsync`.
- **Human (pending — Unity Test Runner, EditMode, never batchmode):** run `DSMManagerWindowStateTests` (5), then the full Phase 1–5 suite. Also confirm in the DSM Manager window that deleting the selected slot now lands on the default slot. This is the TEST-05 / criterion-5 acceptance gate and the phase's verification closer.

## Next Phase Readiness
- Phase 5 implementation is complete: all 8 requirements (WATCH-01, PERF-01..04, EDIT-01, BUGS-01, TEST-05) have shipped code plus authored tests.
- The whole phase now sits behind one human gate: run the full EditMode suite in Unity. Every plan's automated (non-Unity) verification has passed.

---
*Phase: 05-performance-reactivity-editor-tooling*
*Completed: 2026-07-22*
