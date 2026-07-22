---
phase: 05-performance-reactivity-editor-tooling
plan: 01
subsystem: runtime-reactivity
tags: [unitask, playerloop, channels, watchasync, batching, nunit, editmode]

requires:
  - phase: 03-schema-validation
    provides: lenient/strict schema coercion in DSMSlot.Set and the coerced-notify path WR-05 asserts
  - phase: 01-foundation
    provides: DSMWatcher channel-per-subscriber model and DSMSlot.WatchAsync
provides:
  - Per-frame batched WatchAsync notifications that coalesce to the latest value per key
  - DSMWatcher.Flush() as a deterministic frame boundary for tests
  - DSMSlot.FlushWatchers() test hook plus InternalsVisibleTo for DMS.Tests.Editor
  - Regression coverage closing the deferred Phase-03 CR-01 and WR-05 review items
affects: [editor-tooling, ui-consumers, future-watch-features]

tech-stack:
  added: []
  patterns:
    - "Notify buffers, Flush delivers: notification producers never write to subscriber channels on the caller's thread"
    - "PlayerLoop-scheduled work exposes an internal deterministic driver so EditMode tests never depend on a ticking loop"

key-files:
  created:
    - Tests/Editor/DSMWatcherBatchingTests.cs
    - Runtime/AssemblyInfo.cs
  modified:
    - Runtime/DSMWatcher.cs
    - Runtime/DSMSlot.cs
    - Tests/Editor/DSMSchemaValidationTests.cs

key-decisions:
  - "Flush snapshots (channel, value) pairs under the lock and writes outside it, preserving DSMWatcher's original lock discipline"
  - "The no-synchronous-delivery test asserts on the Set() thread without yielding, because the editor update loop drives the scheduled flush and any await would make it flaky"
  - "Runtime/AssemblyInfo.cs grants InternalsVisibleTo(\"DMS.Tests.Editor\") rather than widening FlushWatchers to public"

patterns-established:
  - "Coalesce-to-latest buffering: Dictionary<string, object> pending + a single dirty flag per watcher, no global registry"
  - "EditMode async collectors use UniTask.Void + a bounded CancellationTokenSource, never Assert.ThrowsAsync"

requirements-completed: [WATCH-01]

coverage:
  - id: D1
    description: "Many Set()s of one key in a frame deliver exactly one WatchAsync notification carrying the latest value"
    requirement: WATCH-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMWatcherBatchingTests.cs#Set_ManyTimesSameKeyInOneFrame_DeliversLatestValueOnce"
        status: unknown
    human_judgment: true
    rationale: "Project rule forbids running Unity tests from the agent; the EditMode suite is the human acceptance gate for WATCH-01"
  - id: D2
    description: "Batching is per key and the pending buffer clears each frame; initial current-value replay on subscribe is preserved"
    requirement: WATCH-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMWatcherBatchingTests.cs#Set_DistinctKeysInOneFrame_EachSubscriberGetsItsOwnValueOnce, Set_AcrossTwoFrames_DeliversOneValuePerFrame, Subscribe_WhenKeyAlreadyHasValue_ReplaysCurrentValueImmediately"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner gate — cannot be executed by the agent"
  - id: D3
    description: "Notify never writes to subscriber channels inline; the only channel write lives in Flush()"
    requirement: WATCH-01
    verification:
      - kind: other
        ref: "grep -c 'Writer.TryWrite' Runtime/DSMWatcher.cs == 1 && dotnet build DMS.Runtime.csproj -clp:ErrorsOnly"
        status: pass
    human_judgment: false
  - id: D4
    description: "Deferred Phase-03 CR-01 (no value leak in the lenient coercion warning) and WR-05 (coerced Set reaches a schema-typed subscriber) are locked by regression tests"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Lenient_CoercionFailure_Warning_DoesNotLeakOffendingValue, Coerced_Set_Reaches_SchemaTyped_WatchAsync_Subscriber"
        status: unknown
    human_judgment: true
    rationale: "EditMode tests authored but not run by the agent per project instructions"

duration: 18 min
completed: 2026-07-22
status: complete
---

# Phase 5 Plan 01: Batched WatchAsync Notifications Summary

**DSMWatcher now buffers the latest value per key and delivers one coalesced notification per key per PostLateUpdate flush, with a deterministic `FlushWatchers()` frame boundary for EditMode tests and regression coverage closing Phase-03's CR-01/WR-05.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-07-22T13:56:00Z
- **Completed:** 2026-07-22T14:14:00Z
- **Tasks:** 3
- **Files modified:** 5 (2 created, 3 modified; plus 2 .meta and the git-ignored DMS.Runtime.csproj)

## Accomplishments
- `DSMWatcher.Notify` no longer writes to subscriber channels on the `Set()` thread — it stores the latest value per key under the existing lock and schedules a single `PostLateUpdate` flush, so 200 `Set("hp", …)` calls in one frame wake subscribers once with the final value (WATCH-01).
- `internal void Flush()` drains the pending buffer, delivering exactly one value per key to every registered channel; snapshots are taken under the lock and written outside it, matching the original lock discipline.
- `DSMSlot.FlushWatchers()` gives tests (and any deterministic caller) a frame boundary that does not require a ticking PlayerLoop.
- Five EditMode cases pin the contract: coalesce-to-latest, per-key independence, per-frame buffer clearing, preserved initial-value replay, and no synchronous delivery before flush.
- The two deferred Phase-03 review items are now regression-tested with zero production-code change: CR-01's no-value-leak invariant and WR-05's coerced-notify delivery.

## Task Commits

1. **Task 1: Author failing EditMode batching spec (RED)** — `989d487` (test)
2. **Task 2: Batch DSMWatcher notifications (GREEN)** — `f0c4853` (feat)
3. **Task 3: CR-01 + WR-05 regression tests** — `ac46d21` (test)

## Files Created/Modified
- `Runtime/DSMWatcher.cs` — pending-value buffer, dirty flag, `ScheduleFlush()`, `internal Flush()`; `Notify` reduced to buffer + schedule
- `Runtime/DSMSlot.cs` — `internal void FlushWatchers()`; `Set`'s `_watcher.Notify` call site unchanged
- `Runtime/AssemblyInfo.cs` — `InternalsVisibleTo("DMS.Tests.Editor")`
- `Tests/Editor/DSMWatcherBatchingTests.cs` — 5-case batching fixture
- `Tests/Editor/DSMSchemaValidationTests.cs` — CR-01 and WR-05 regression tests

## Decisions Made
- Delivery snapshots are built as `(channel, value)` pairs inside the lock and written after releasing it, so a slow subscriber can never block a `Set()`.
- The "no synchronous delivery" assertion runs on the `Set()` thread with no intervening `await`: UniTask forces editor player-loop updates via `EditorApplication.update`, so any yield could legitimately deliver the buffered value and make the test flaky.
- `FlushWatchers()` stays `internal`; the test assembly gets access through `InternalsVisibleTo` rather than growing the public API.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test assembly could not see `internal` members**
- **Found during:** Task 2 (batched DSMWatcher)
- **Issue:** The plan specifies `internal void FlushWatchers()` on `DSMSlot`, but tests live in the separate `DMS.Tests.Editor` assembly and no `InternalsVisibleTo` existed anywhere in the project — the new fixture would not compile.
- **Fix:** Added `Runtime/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("DMS.Tests.Editor")]` (plus its `.meta`), keeping the member `internal` as planned instead of widening it to `public`.
- **Files modified:** Runtime/AssemblyInfo.cs, Runtime/AssemblyInfo.cs.meta, DMS.Runtime.csproj (git-ignored, Compile Include entry)
- **Verification:** `dotnet build DMS.Runtime.csproj -clp:ErrorsOnly` → 0 errors
- **Committed in:** `f0c4853` (Task 2 commit)

**2. [Rule 1 - Test correctness] Replaced the planned bounded wait in the deferral test**
- **Found during:** Task 1 (batching spec)
- **Issue:** The plan suggested "a short bounded wait/yield" before asserting that nothing was delivered pre-flush. In EditMode, UniTask pumps the player loop from `EditorApplication.update`, so the scheduled flush would fire during that wait and the assertion would fail intermittently.
- **Fix:** Assert on the `Set()` thread with no intervening await — which is exactly the invariant WATCH-01 adds (Notify must not write inline) — with a comment recording why no yield is used.
- **Files modified:** Tests/Editor/DSMWatcherBatchingTests.cs
- **Verification:** Deterministic under both a ticking and a idle editor loop; the coalescing test remains the primary RED signal.
- **Committed in:** `989d487` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 test correctness)
**Impact on plan:** Both were necessary to make the planned design compile and test deterministically. No scope creep; the public API and `Set()` semantics are unchanged.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Verification Status

- **Automated (no Unity):** `dotnet build DMS.Runtime.csproj -clp:ErrorsOnly` → Build succeeded, 0 errors. Greps confirm `_pending`/`_flushScheduled` exist, `internal void Flush()` exists, `Writer.TryWrite` appears exactly once (inside `Flush`), `DSMSlot.FlushWatchers()` exists, `_watcher.Notify(key, notifyValue)` in `Set` is unchanged, `Watch<T>`'s replay yield is intact, both new test names are present, and the lenient coercion catch still has no `ex.Message` interpolation.
- **Human (pending — project rule forbids agent-run Unity tests):** In Unity Test Runner (EditMode, never batchmode/`-runTests`) run `DSMWatcherBatchingTests` (5 tests), the two new `DSMSchemaValidationTests`, and the full Phase 1–4 suite — especially `DSMSlotConcurrencyTests.MultiWatcher_AllSubscribersReceiveValue`, which now depends on the PlayerLoop flush rather than synchronous delivery. This is the WATCH-01 acceptance gate.

## Next Phase Readiness
- Runtime reactivity work for the phase is complete; 05-02 (`GetAllSlots` cache + UniTask pin) and 05-03 (Editor window decomposition) touch disjoint files and can proceed.
- `InternalsVisibleTo("DMS.Tests.Editor")` is now available to later plans that need deterministic test hooks (relevant to 05-04/05-05).
- Open: `MultiWatcher_AllSubscribersReceiveValue` fires `Set()` then awaits delivery without an explicit flush. It should pass on a ticking editor loop; if the human run shows it hanging until the 5 s timeout, add a `FlushWatchers()` call there.

---
*Phase: 05-performance-reactivity-editor-tooling*
*Completed: 2026-07-22*
