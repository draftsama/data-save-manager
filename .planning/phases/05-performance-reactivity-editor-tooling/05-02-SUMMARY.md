---
phase: 05-performance-reactivity-editor-tooling
plan: 02
subsystem: runtime-performance
tags: [caching, filesystem, upm, unitask, dependency-pinning, nunit, editmode]

requires:
  - phase: 01-foundation
    provides: DSMSlotManager slot registry, _slotsLock thread-safety invariant, GetAllSlots disk scan
  - phase: 02-encryption
    provides: RotateEncryptionKeyAsync, the heaviest GetAllSlots caller
provides:
  - Cached GetAllSlots() with create/delete-only invalidation
  - Lock-consistent slot enumeration (scan moved under _slotsLock)
  - Reproducible UniTask resolution via an immutable release-tag pin
affects: [editor-tooling, key-rotation, ci-reproducibility]

tech-stack:
  added: []
  patterns:
    - "Nullable-field cache guarded by the owning object's existing lock; invalidate on mutation, never on read"
    - "UPM git dependencies pinned as ?path=<sub>#<release-tag>"

key-files:
  created:
    - Tests/Editor/DSMSlotManagerCacheTests.cs
  modified:
    - Runtime/DSMSlotManager.cs
    - package.json

key-decisions:
  - "DeleteSlot invalidates after the save files are deleted, not before, so a concurrent re-scan cannot cache the deleted slot back in"
  - "The empty-directory result is cached as well, so a missing save directory stops re-stating on every call"
  - "GetAllSlots' disk scan moved inside _slotsLock, making enumeration consistent with concurrent create/delete rather than racing them"
  - "Pinned UniTask to 2.5.11, the newest non-prerelease tag resolved live from git ls-remote --tags"

patterns-established:
  - "Cache invalidation points are the mutation sites only: DeleteSlot and the new-slot branch of GetOrCreateSlot"
  - "Cache tests assert public behavior (create/delete/list) instead of reflecting into the private cache field"

requirements-completed: [PERF-03, PERF-04]

coverage:
  - id: D1
    description: "GetAllSlots() serves a cached array and only re-scans the save directory after a slot is created or deleted through the manager"
    requirement: PERF-03
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotManagerCacheTests.cs#GetAllSlots_AfterCreatingSlotThroughManager_IncludesNewSlot, GetAllSlots_AfterDeleteSlot_DropsDeletedSlot, GetAllSlots_CalledTwiceWithoutChanges_ReturnsSameSet"
        status: unknown
    human_judgment: true
    rationale: "Project rule forbids running Unity tests from the agent; the EditMode suite is the PERF-03 acceptance gate"
  - id: D2
    description: "Caching never hides pre-existing on-disk slots, and read-only slot access never disturbs the cached set"
    requirement: PERF-03
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSlotManagerCacheTests.cs#GetAllSlots_FirstCallOnFreshManager_SeesPreExistingSaveFile, GetAllSlots_AfterReadOnlyAccessToExistingSlot_KeepsSameSet"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner gate — cannot be executed by the agent"
  - id: D3
    description: "Cache field exists, is guarded by the existing _slotsLock, and is invalidated at both mutation sites"
    requirement: PERF-03
    verification:
      - kind: other
        ref: "dotnet build DMS.Runtime.csproj -clp:ErrorsOnly && grep -c '_allSlotsCache = null' Runtime/DSMSlotManager.cs == 2"
        status: pass
    human_judgment: false
  - id: D4
    description: "com.cysharp.unitask resolves an immutable release tag instead of the default branch"
    requirement: PERF-04
    verification:
      - kind: other
        ref: "node -e regex check on package.json → pinned: …/UniTask#2.5.11"
        status: pass
    human_judgment: true
    rationale: "Only Unity's package resolver can prove the pinned tag actually resolves and compiles; the agent cannot trigger it"

duration: 12 min
completed: 2026-07-22
status: complete
---

# Phase 5 Plan 02: GetAllSlots Caching and UniTask Pin Summary

**`DSMSlotManager.GetAllSlots()` now serves a `_slotsLock`-guarded cached array invalidated only by slot create/delete, and `com.cysharp.unitask` is pinned to release tag 2.5.11 instead of tracking the default branch.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-22T14:14:00Z
- **Completed:** 2026-07-22T14:26:00Z
- **Tasks:** 3
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- `GetAllSlots()` walks the save directory once per slot-set change instead of on every call — the Editor window and `RotateEncryptionKeyAsync` (which enumerates all slots) both benefit without a signature change.
- Invalidation is confined to the two real mutation points: `DeleteSlot` (after the files are removed) and the new-slot branch of `GetOrCreateSlot`. The existing-slot early-return and every read path leave the cache alone.
- The scan now runs under the existing `_slotsLock`, so enumeration is consistent with concurrent create/delete rather than racing them — the Phase-1 thread-safety invariant is preserved with no second lock.
- `com.cysharp.unitask` is pinned to `#2.5.11`, so two identical checkouts can no longer resolve different UniTask code.
- Five EditMode cases pin the caching contract through public behavior only, with no reflection into the private field.

## Task Commits

1. **Task 1: Behavioral spec for caching + invalidation (RED)** — `2b876c6` (test)
2. **Task 2: Cache GetAllSlots with create/delete invalidation** — `90c9f30` (feat)
3. **Task 3: Pin the UniTask UPM dependency** — `96dd5e8` (feat)

## Files Created/Modified
- `Runtime/DSMSlotManager.cs` — `private string[]? _allSlotsCache`; `GetAllSlots` reads/fills it under `_slotsLock`; invalidation in `DeleteSlot` and the new-slot branch of `GetOrCreateSlot`
- `package.json` — `com.cysharp.unitask` gains the `#2.5.11` fragment after the `?path=` query
- `Tests/Editor/DSMSlotManagerCacheTests.cs` — 5-case caching fixture

## Decisions Made
- `DeleteSlot` invalidates **after** deleting the `.json`/`.enc` files. Invalidating before the deletes leaves a window where a concurrent `GetAllSlots()` re-scans, still sees the files, and caches the deleted slot back in.
- The `Array.Empty<string>()` no-directory result is cached too; a later create still invalidates through `GetOrCreateSlot`, so nothing is lost and a missing directory stops being re-stat'ed on every call.
- Pinned tag resolved live rather than using the plan's offline fallback: `git ls-remote --tags` gave 2.5.11 as the newest non-prerelease tag, one above the plan's `2.5.10` suggestion.
- `GetAllSlots()` returns the cached array instance itself, as the plan specifies. No current caller mutates it (`DSM.GetAllSlots().Contains(...)`, rotation's `.Where(...)`), but note that a caller who sorts the result in place would now be mutating shared state.

## Deviations from Plan

None - plan executed exactly as written. (Task 3's offline fallback to `2.5.10` was not needed: the upstream tag list was reachable and 2.5.11 is newer.)

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. Note the Unity re-resolve step under Verification Status below.

## Verification Status

- **Automated (no Unity):** `dotnet build DMS.Runtime.csproj -clp:ErrorsOnly` → Build succeeded, 0 errors. `grep -c '_allSlotsCache = null'` → 2 (DeleteSlot + the new-slot branch of GetOrCreateSlot). The node regex check confirms `UniTask.git?path=…#2.5.11`, and `git diff --stat package.json` shows exactly one changed line.
- **Human (pending — project rule forbids agent-run Unity tests):** In Unity Test Runner (EditMode, never batchmode/`-runTests`) run `DSMSlotManagerCacheTests`, plus `DSMKeyRotationTests` and `DSMSlotConcurrencyTests` — rotation enumerates every slot through `GetAllSlots()`, so it must still rotate all of them with the cache in place. Then re-open the project (or Packages ▸ Resolve) so Unity resolves UniTask at the pinned tag and the whole package still compiles. This is the PERF-03/PERF-04 acceptance gate.

## Next Phase Readiness
- No file overlap with 05-01; 05-03 (Editor window decomposition) is unblocked and touches only `Editor/`.
- 05-04's version/migration panel will call slot enumeration from the Editor — it now hits the cache, and any slot it creates invalidates correctly through `GetOrCreateSlot`.

---
*Phase: 05-performance-reactivity-editor-tooling*
*Completed: 2026-07-22*
