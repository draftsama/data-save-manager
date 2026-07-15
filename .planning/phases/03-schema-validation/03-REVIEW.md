---
phase: 03-schema-validation
reviewed: 2026-07-15T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - Runtime/DSMConfig.cs
  - Runtime/DSMSchema.cs
  - Runtime/DSMSlot.cs
  - Tests/Editor/DSMSchemaValidationTests.cs
  - Tests/Editor/DSMTestConfig.cs
findings:
  critical: 1
  warning: 4
  info: 3
  total: 8
status: issues_found
---

# Phase 03: Code Review Report

**Reviewed:** 2026-07-15
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 03 adds schema validation: `DSMSchema` reflects `DSMConstant` public-static fields into a
key→CLR-type map cached per `Type` behind a lock, `DSMConfig.StrictSchema` gates a throw-vs-coerce
policy, and `DSMSlot.Set<T>/Get<T>` consult the schema. The reflection/caching is thread-safe, the
happy-path coercion round-trip is correct, and null/empty-schema pass-through works as tested.

However, the **security-sensitive no-value-leak invariant is violated on the lenient coercion-failure
path** (Critical). Several robustness asymmetries exist between the carefully-guarded lenient `Set`
and the unguarded lenient `Get`, and a pre-existing unlocked read of `_data` in `WatchAsync` is a real
concurrency defect in a file now under review. The strict-mode type check uses exact type equality,
which will produce false-positive violations for assignable/compatible types.

The provided tests exercise strict/lenient throw/coerce, unconstrained pass-through, and the
strict-mode leak case — but there is **no test covering the lenient coercion-failure log for value
leakage** (the exact path that leaks), so the invariant regression is untested.

## Critical Issues

### CR-01: Lenient coercion-failure warning leaks the attempted value

**File:** `Runtime/DSMSlot.cs:57`
**Issue:** The security invariant states no warning/exception message may leak a stored or attempted
value — only key name and type names. In the lenient coercion catch block, the log interpolates the
underlying Newtonsoft exception message:

```csharp
Debug.LogWarning($"DSM: could not coerce value for key '{key}': {ex.Message}");
```

`ex` here is the exception thrown by `JToken.FromObject(value).ToObject(expected)`. Newtonsoft
conversion errors routinely embed the offending value in their `Message`, e.g.:
- string→bool: `Could not convert string to boolean: <value>. Path ''.`
- string→enum: `Requested value '<value>' was not found.`
- string→Guid/decimal/DateTime: messages that quote the input.

So a `Set("apiToken", "<secret>")` against an `int`/`bool`/enum schema key writes the secret straight
into the Unity log. This is the precise leak the invariant forbids, on the one path that handles
untrusted/mismatched input. The existing leak test (`Strict_ExceptionMessage_DoesNotLeakOffendingValue`)
only covers the *strict* exception message, not this lenient warning, so it does not catch this.

**Fix:** Never interpolate `ex.Message` (or the value) into the log. Log key + type names only, and at
most the exception type name:

```csharp
catch (Exception ex)
{
    Debug.LogWarning(
        $"DSM: could not coerce key '{key}' from '{typeof(T).Name}' to '{expected.Name}' " +
        $"({ex.GetType().Name}) — storing as-is.");
    token = JToken.FromObject(value, _serializer.JsonSerializer);
}
```

Add a regression test asserting the lenient warning (captured via `LogAssert`) does not contain the
offending value, mirroring the strict-mode test.

## Warnings

### WR-01: `WatchAsync` reads `_data` without holding `_dataLock`

**File:** `Runtime/DSMSlot.cs:351-357`
**Issue:** Every other reader/writer of `_data` takes `_dataLock`, but the watch poll callback reads it
unlocked:

```csharp
if (!_data.TryGetValue(key, out var token)) return (false, default!);
var value = token.ToObject<T>(_serializer.JsonSerializer);
```

This races `Set` (`_data[key] = token` under lock) and, worse, `Load`/`LoadAsync` which *replace the
whole dictionary reference* (`_data = deserialized`) under lock. A concurrent `Dictionary` read during a
write/reassignment is undefined behavior and can throw `InvalidOperationException` or return corrupt
state. Pre-existing, but this file is under review and the defect is real.

**Fix:** Take the lock for the snapshot read:

```csharp
_watcher.Watch<T>(key, () =>
{
    JToken? token;
    lock (_dataLock) { if (!_data.TryGetValue(key, out token)) return (false, default!); }
    var value = token.ToObject<T>(_serializer.JsonSerializer);
    return value is not null ? (true, value) : (false, default!);
});
```

### WR-02: Lenient `Get` is not "never throws" and can propagate a value-bearing exception

**File:** `Runtime/DSMSlot.cs:82-90`
**Issue:** In lenient mode `Set` is carefully wrapped so it never throws, but the symmetric `Get` only
logs a warning and then does an unguarded conversion:

```csharp
return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
```

If the stored token cannot convert to `T` (e.g. schema key stored as one type, caller requests an
incompatible `T`), `ToObject<T>` throws an unhandled Newtonsoft exception. This breaks the lenient
"warn, don't fail" contract and, because the thrown message can embed the stored value, surfaces a
value leak to whatever logs the uncaught exception (same invariant as CR-01, via propagation).

**Fix:** Guard the conversion; on failure warn (key + type names only) and return `defaultValue`:

```csharp
try { return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue; }
catch (Exception ex)
{
    Debug.LogWarning($"DSM: key '{key}' could not be read as '{typeof(T).Name}' ({ex.GetType().Name}).");
    return defaultValue;
}
```

### WR-03: Exact-type equality causes false-positive schema violations for compatible types

**File:** `Runtime/DSMSlot.cs:42` and `77`
**Issue:** Both gates use `typeof(T) != expected`, i.e. reference-exact type identity. Any assignable or
compatible type is treated as a mismatch: expected `object` always "mismatches", a derived type against
a base-class schema field "mismatches", `List<int>` against an `IList` field "mismatches". In strict
mode this throws `DSMSchemaViolationException` for values that are perfectly valid to store, and in
lenient mode it forces an unnecessary coercion round-trip. For the current constant set (int/string/
Vector2) this is latent, but it is a correctness trap the moment a schema field is a non-sealed or
interface type.

**Fix:** Use assignability rather than identity:

```csharp
if (_schema.TryGetExpectedType(key, out var expected) && !expected.IsAssignableFrom(typeof(T)))
```

### WR-04: Lenient `Set` fallback conversion is outside the guard and can still throw

**File:** `Runtime/DSMSlot.cs:58`
**Issue:** The catch block's fallback re-serializes the raw value:

```csharp
token = JToken.FromObject(value, _serializer.JsonSerializer);
```

This call is inside the `catch` but is not itself guarded. If `value` is unserializable by Newtonsoft
(e.g. a self-referencing object graph, or a type with no converter), `FromObject` throws and escapes
`Set`, breaking the documented "lenient mode never throws" invariant on line 55. (The non-mismatch
`else` branch at line 63 has the same exposure, but that is pre-existing normal-path behavior.)

**Fix:** Either wrap the fallback in its own try/catch (drop the write and warn on failure) or document
explicitly that lenient mode still throws on fundamentally unserializable values — the current comment
promising it "never throws" is inaccurate.

## Info

### IN-01: Redundant exception handler in `DSMSchema.Build`

**File:** `Runtime/DSMSchema.cs:48-57`
**Issue:** `catch (ReflectionTypeLoadException ex)` and the following `catch (Exception ex)` have
identical bodies. `ReflectionTypeLoadException` is a subclass of `Exception`, so the first handler adds
no distinct behavior — dead/duplicated code.
**Fix:** Remove the `ReflectionTypeLoadException` handler and keep the single `catch (Exception)`, or
give the specific handler distinct handling if one is intended.

### IN-02: `SeedDefaults` logs `ex.Message`, which can embed a default value

**File:** `Runtime/DSMSlot.cs:370`
**Issue:** `Debug.LogWarning($"DSM: Could not seed default for '{field.Name}': {ex.Message}")` follows
the same value-in-message pattern as CR-01. Lower severity because seeded defaults come from
compile-time `DSMConstant` fields (public by construction, not secrets), but it is the same
invariant class and worth aligning to key/type-name-only logging for consistency.
**Fix:** Log `field.Name` + `ex.GetType().Name` only.

### IN-03: Schema reflects *all* public static fields, including non-key helpers

**File:** `Runtime/DSMSchema.cs:44-45`
**Issue:** `GetFields(BindingFlags.Public | BindingFlags.Static)` maps every public static field into the
schema. If `DSMConstant` (a `partial` class users extend) ever gains a public static helper/cache field
that is not a save key, it silently becomes a validated schema key and a seeded default. This is a
design sharp-edge given the class is user-authored/partial.
**Fix:** Consider constraining to literal/`readonly` fields, or an opt-in marker attribute, and document
that every public static field of `DSMConstant` is treated as a save-key schema entry.

---

_Reviewed: 2026-07-15_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
