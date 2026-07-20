---
phase: 03-schema-validation
reviewed: 2026-07-20T15:06:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - Runtime/DSMSchema.cs
  - Runtime/DSMSlot.cs
  - Runtime/DSMConfig.cs
  - Tests/Editor/DSMSchemaValidationTests.cs
  - Tests/Editor/DSMTestConfig.cs
findings:
  critical: 1
  warning: 5
  info: 3
  total: 9
status: issues_found
---

# Phase 03: Code Review Report (re-review)

**Reviewed:** 2026-07-20T15:06:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

This is a **re-review** of phase 03 (schema validation) to verify whether the
previously deferred CRITICAL secret-leakage finding and the WR-02/WR-04 unguarded
`JToken` warnings from the prior REVIEW.md still exist in the current code.

Verdict: **all prior findings still exist unchanged.** The source (`DSMSlot.cs`,
`DSMSchema.cs`, `DSMConfig.cs`) has not been modified since the first review — the
coercion-failure log at `DSMSlot.cs:57` still interpolates `ex.Message` (CR-01),
the unguarded `Get` (WR-02), unlocked `_data` read in `WatchAsync` (WR-01),
exact-type-equality gate (WR-03), and unguarded lenient `Set` fallback (WR-04) are
all present. The one leak test still covers only the strict throw path, not the
lenient log path where the leak occurs, so the invariant regression remains
untested.

This pass also surfaces **one new defect the prior review missed** (WR-05): in the
coercion path `Set` notifies watchers with the *uncoerced* original value, which
the watcher's `is T` type filter then silently drops — so schema-coerced writes
never reach `WatchAsync<T>` subscribers.

The reflection/caching remains thread-safe and the happy-path coercion round-trip
is still correct; findings are confined to security (value leakage) and robustness
(unguarded conversions, notification consistency, concurrency).

## Critical Issues

### CR-01: Lenient coercion-failure warning leaks the attempted value (STILL PRESENT — T-03-01)

**File:** `Runtime/DSMSlot.cs:57`
**Issue:** The no-value-leak invariant (T-03-01) states no warning/exception
message may leak a stored or attempted value — only key and type names. In the
lenient coercion catch block, the log interpolates the underlying Newtonsoft
exception message:

```csharp
Debug.LogWarning($"DSM: could not coerce value for key '{key}': {ex.Message}");
```

`ex` is thrown by `JToken.FromObject(value).ToObject(expected)`. Newtonsoft
conversion errors routinely embed the offending value in `Message`, e.g.:
- string→bool: `Could not convert string to boolean: <value>. Path ''.`
- string→enum: `Requested value '<value>' was not found.`
- string→Guid/decimal/DateTime: messages that quote the input.

So `Set("apiToken", "<secret>")` against an `int`/`bool`/enum schema key writes
the secret straight into the Unity log. The existing test
`Strict_ExceptionMessage_DoesNotLeakOffendingValue` only covers the *strict*
`DSMSchemaViolationException` message (value-free by construction) and never
exercises this lenient warning, so it does not catch this. Deferred at first
review; still unfixed.

**Fix:** Never interpolate `ex.Message` (or the value). Log key + type names only,
at most the exception type name:

```csharp
catch (Exception ex)
{
    Debug.LogWarning(
        $"DSM: could not coerce key '{key}' from '{typeof(T).Name}' to '{expected.Name}' " +
        $"({ex.GetType().Name}) — storing as-is.");
    token = JToken.FromObject(value, _serializer.JsonSerializer);
}
```

Add a regression test asserting the lenient warning (captured via `LogAssert`)
does not contain the offending value, mirroring the strict-mode test.

## Warnings

### WR-01: `WatchAsync` reads `_data` without holding `_dataLock` (STILL PRESENT)

**File:** `Runtime/DSMSlot.cs:351-357`
**Issue:** Every other reader/writer of `_data` takes `_dataLock`, but the watch
poll callback reads it unlocked:

```csharp
if (!_data.TryGetValue(key, out var token)) return (false, default!);
var value = token.ToObject<T>(_serializer.JsonSerializer);
```

This races `Set` (`_data[key] = token` under lock) and, worse, `Load`/`LoadAsync`
which *replace the whole dictionary reference* (`_data = deserialized`) under
lock. A concurrent `Dictionary` read during a write/reassignment is undefined
behavior and can throw `InvalidOperationException` or observe corrupt state. The
`ToObject<T>` is also unguarded here.

**Fix:** Snapshot under the lock, convert outside it in a try/catch:

```csharp
_watcher.Watch<T>(key, () =>
{
    JToken? token;
    lock (_dataLock) { if (!_data.TryGetValue(key, out token)) return (false, default!); }
    try { var v = token.ToObject<T>(_serializer.JsonSerializer); return v is not null ? (true, v) : (false, default!); }
    catch { return (false, default!); }
});
```

### WR-02: Lenient `Get` is not "never throws" and can propagate a value-bearing exception (STILL PRESENT)

**File:** `Runtime/DSMSlot.cs:82-90`
**Issue:** In lenient mode `Set` is wrapped so it never throws, but the symmetric
`Get` only logs a warning then does an unguarded conversion:

```csharp
return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
```

This is directly reachable: after a lenient uncoercible `Set` stores a raw string
under an `int`-schema key (CR-01's fallback at line 58), a later `Get<int>(key, 0)`
calls `ToObject<int>` on that string token and throws instead of returning the
default. This breaks the "warn, don't fail" contract and, because the thrown
message can embed the stored value, re-surfaces the CR-01 leak via propagation.

**Fix:** Guard the conversion; on failure warn (key + type names only) and return
`defaultValue`:

```csharp
try { return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue; }
catch (Exception ex)
{
    Debug.LogWarning($"DSM: key '{key}' could not be read as '{typeof(T).Name}' ({ex.GetType().Name}).");
    return defaultValue;
}
```

### WR-03: Exact-type equality causes false-positive schema violations for compatible types (STILL PRESENT)

**File:** `Runtime/DSMSlot.cs:42` and `:77`
**Issue:** Both gates use `typeof(T) != expected` — reference-exact type identity.
Any assignable/compatible type is treated as a mismatch: expected `object` always
"mismatches", a derived type against a base-class schema field "mismatches",
`List<int>` against an `IList` field "mismatches". In strict mode this throws
`DSMSchemaViolationException` for perfectly valid values; in lenient mode it forces
an unnecessary coercion round-trip. Latent for the current int/string/Vector2
constant set, but a correctness trap the moment a schema field is a non-sealed or
interface type.

**Fix:** Use assignability rather than identity:

```csharp
if (_schema.TryGetExpectedType(key, out var expected) && !expected.IsAssignableFrom(typeof(T)))
```

### WR-04: Lenient `Set` fallback conversion is outside the guard and can still throw (STILL PRESENT)

**File:** `Runtime/DSMSlot.cs:58`
**Issue:** The catch block's fallback re-serializes the raw value:

```csharp
token = JToken.FromObject(value, _serializer.JsonSerializer);
```

This is inside the `catch` but is not itself guarded. If `value` is unserializable
by Newtonsoft (self-referencing graph, no converter), `FromObject` throws and
escapes `Set`, breaking the documented "lenient mode never throws" invariant on
line 55. (The non-mismatch `else` branch at line 63 has the same exposure as
pre-existing normal-path behavior.)

**Fix:** Wrap the fallback in its own try/catch (drop the write and warn on
failure) or correct the comment — the current "never throws" promise is
inaccurate.

### WR-05: Coercion path notifies watchers with the uncoerced value, which is silently dropped (NEW)

**File:** `Runtime/DSMSlot.cs:70` (with `Runtime/DSMWatcher.cs:31,42`)
**Issue:** `Set` stores the (possibly coerced) `token` but calls
`_watcher.Notify(key, value)` with the *original* `value`. In the coercion case —
e.g. `Set<string>("hp", "42")` where the schema type is `int` — storage holds the
int token `42`, but `Notify` pushes the string `"42"`. `DSMWatcher.Notify` forwards
it as `object` (`DSMWatcher.cs:42`) and `Watch<T>` filters with
`if (value is T typedValue)` (`DSMWatcher.cs:31`). A `WatchAsync<int>("hp")`
subscriber therefore fails the `is int` test and the notification is **silently
dropped**, even though a new value was persisted. Watchers observe a different
value/type than `Get` returns for the same key.

**Fix:** Notify with the coerced result so subscribers see what was actually
stored — push the coerced object used to build `token` (or `token.ToObject`-typed
value) rather than the raw `value`, so the runtime type matches the stored token
for schema-constrained keys.

## Info

### IN-01: Redundant exception handler in `DSMSchema.Build` (STILL PRESENT)

**File:** `Runtime/DSMSchema.cs:48-57`
**Issue:** `catch (ReflectionTypeLoadException ex)` and the following
`catch (Exception ex)` have identical bodies. `ReflectionTypeLoadException` is a
subclass of `Exception`, so the first handler adds no distinct behavior —
dead/duplicated code.
**Fix:** Remove the `ReflectionTypeLoadException` handler, or give it distinct
handling (e.g. log `ex.LoaderExceptions`).

### IN-02: `SeedDefaults` logs `ex.Message`, which can embed a default value (STILL PRESENT)

**File:** `Runtime/DSMSlot.cs:370`
**Issue:** `Debug.LogWarning($"DSM: Could not seed default for '{field.Name}': {ex.Message}")`
follows the same value-in-message pattern as CR-01. Lower severity because seeded
defaults come from compile-time constant fields (public by construction, not
secrets), but it is the same invariant class and worth aligning to
key/type-name-only logging.
**Fix:** Log `field.Name` + `ex.GetType().Name` only.

### IN-03: Schema reflects *all* public static fields, including non-key helpers (STILL PRESENT)

**File:** `Runtime/DSMSchema.cs:44-45`
**Issue:** `GetFields(BindingFlags.Public | BindingFlags.Static)` maps every public
static field into the schema. If the user-authored constant type ever gains a
public static helper/cache field that is not a save key, it silently becomes a
validated schema key and a seeded default — a design sharp-edge for a
partial/user-extended class.
**Fix:** Constrain to literal/`readonly` fields or an opt-in marker attribute, and
document that every public static field is treated as a save-key schema entry.

---

_Reviewed: 2026-07-20T15:06:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard (re-review)_
