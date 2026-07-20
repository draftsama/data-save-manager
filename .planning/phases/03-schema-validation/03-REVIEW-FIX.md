---
phase: 03-schema-validation
fixed_at: 2026-07-20T15:15:00Z
review_path: .planning/phases/03-schema-validation/03-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 03: Code Review Fix Report

**Fixed at:** 2026-07-20T15:15:00Z
**Source review:** .planning/phases/03-schema-validation/03-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 6 (1 critical + 5 warning)
- Fixed: 6
- Skipped: 0

**Note on verification:** This is a Unity project; per project constraint no Unity
tests were run and no `dotnet build` was possible (Unity/Newtonsoft/UniTask
assemblies are not available outside the editor). Fixes were verified by re-reading
the modified regions and by definite-assignment / control-flow reasoning. The
behavioral fixes (WR-01 concurrency, WR-05 watcher notification) should be
confirmed by the human running the EditMode suite. The review also recommended two
new regression tests (see Follow-ups).

## Fixed Issues

### CR-01: Lenient coercion-failure warning leaks the attempted value

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** d04f0da
**Applied fix:** Replaced the `{ex.Message}` interpolation in the lenient coercion
catch block with a value-free message that names only the key, source type
(`typeof(T).Name`), expected type (`expected.Name`), and the exception type name
(`ex.GetType().Name`). This closes the T-03-01 leak where Newtonsoft conversion
errors embed the offending value in `Message`.

### WR-01: `WatchAsync` reads `_data` without holding `_dataLock`

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** 597a355
**Applied fix:** The watch poll callback now snapshots the token under `_dataLock`
(matching every other reader/writer of `_data`) and performs `ToObject<T>` outside
the lock inside a try/catch, returning `(false, default!)` on failure. Eliminates
the data race against `Set`/`Load`/`LoadAsync` (which reassigns the whole dictionary
reference) and guards the previously unguarded conversion.
_Recommend human verification via EditMode run — concurrency behavior._

### WR-02: Lenient `Get` was not "never throws"

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** 48dfb3a
**Applied fix:** Wrapped the `token.ToObject<T>` conversion in `Get` in a try/catch;
on failure it logs a key + type-name-only warning and returns `defaultValue`.
Restores the "warn, don't fail" contract and prevents a value-bearing conversion
exception from re-surfacing the CR-01 leak via propagation.

### WR-03: Exact-type equality caused false-positive schema violations

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** 1ae3e90
**Applied fix:** Both schema gates (in `Set` and `Get`) now use
`!expected.IsAssignableFrom(typeof(T))` instead of `typeof(T) != expected`, so
assignable/compatible types (derived types against a base-class schema field,
`object`, interface fields) no longer trip a false mismatch.

### WR-04: Lenient `Set` fallback conversion could still throw

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** cdbc6da
**Applied fix:** Wrapped the fallback `JToken.FromObject(value, …)` in the catch
block in its own try/catch. If the raw value is unserializable, the write is
dropped with a value-free warning and `Set` returns rather than throwing —
honoring the "lenient mode never throws" invariant.

### WR-05: Coercion path notified watchers with the uncoerced value

**Files modified:** `Runtime/DSMSlot.cs`
**Commit:** ccb2d9b
**Applied fix:** Added a `notifyValue` local (default = the original `value`) that is
set to the coerced object in the successful coercion path, and changed
`_watcher.Notify(key, value)` to `_watcher.Notify(key, notifyValue)`. Now a
schema-coerced write (e.g. `Set<string>("hp","42")` against an `int` key) pushes the
coerced `int` so `WatchAsync<int>("hp")` subscribers pass the `is T` filter instead
of silently dropping the notification. The non-mismatch and uncoercible-fallback
paths still notify with the stored raw value, which is correct.
_Recommend human verification via EditMode run — changes observable watcher
notification behavior._

## Follow-ups (not code changes — recommended by the review)

- **CR-01 regression test:** Add an EditMode test asserting the lenient
  coercion-failure warning (captured via `LogAssert`) does not contain the
  offending value, mirroring `Strict_ExceptionMessage_DoesNotLeakOffendingValue`.
- **WR-05 regression test:** Add a test that a coerced `Set` reaches a
  `WatchAsync<T>` subscriber of the schema type.

## Out of scope (info findings, not addressed)

IN-01 (redundant `ReflectionTypeLoadException` handler), IN-02 (`SeedDefaults`
logs `ex.Message`), and IN-03 (schema reflects all public static fields) were Info
severity and outside the critical_warning fix scope. IN-02 is the same
value-in-message invariant class as CR-01 and is worth aligning in a follow-up.

---

_Fixed: 2026-07-20T15:15:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
