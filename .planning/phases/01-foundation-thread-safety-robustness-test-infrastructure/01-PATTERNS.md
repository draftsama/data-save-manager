# Phase 1: Foundation — Thread-Safety, Robustness & Test Infrastructure - Pattern Map

**Mapped:** 2026-07-08
**Files analyzed:** 8 (2 heavily modified, 1 new runtime utility, 1 modified widget-panel, 4 new test-infra files/dirs)
**Analogs found:** 8 / 8 (all in-repo; no external analogs needed — every changed file already exists, so its own current content is the primary analog, and `DSMWatcher.cs` supplies the only existing lock/thread-safety precedent in the codebase)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `Runtime/DSMSlot.cs` (CHANGED) | model / service (owns in-memory state + I/O) | CRUD + file-I/O | itself (current version) + `Runtime/DSMWatcher.cs` (lock precedent) | exact (self) / role-match (lock pattern) |
| `Runtime/DSMSlotManager.cs` (CHANGED) | service (slot cache orchestration) | CRUD | itself (current version) + `Runtime/DSMWatcher.cs` (lock precedent) | exact (self) / role-match (lock pattern) |
| `Runtime/DSMSlotNameValidator.cs` (NEW) | utility (pure validation) | transform (string in → bool/throw) | `Runtime/DSMPaths.cs` (pure static utility, no Unity API dependency) | role-match |
| `Runtime/DSMRuntimePanel.cs` (CHANGED — BUGS-04 widget-missing warning) | component (MonoBehaviour orchestration) | event-driven | itself (current version); `Runtime/Widgets/IntWidget.cs` (existing missing-component warning convention) | exact (self) / role-match (warning convention) |
| `Tests/Editor/DMS.Tests.Editor.asmdef` (NEW) | config (asmdef) | n/a | `Editor/DSM.Editor.asmdef` (existing asmdef in same package) | role-match |
| `Tests/Runtime/DMS.Tests.Runtime.asmdef` (NEW) | config (asmdef) | n/a | `Runtime/DMS.Runtime.asmdef` (existing asmdef in same package) | role-match |
| `Tests/Editor/DSMSlotConcurrencyTests.cs` (NEW) | test | request-response (async Task assertions) | `.planning/codebase/TESTING.md` recommended NUnit/UniTask patterns (no existing test file in repo — first test ever written) | no-analog (use TESTING.md sketch) |
| `Tests/Editor/DSMSlotNameValidatorTests.cs` / `DSMSlotLoadRobustnessTests.cs` (NEW) | test | request-response | same as above | no-analog (use TESTING.md sketch) |

## Pattern Assignments

### `Runtime/DSMSlot.cs` (CHANGED — model/service, CRUD + file-I/O)

**Analog:** itself (`Runtime/DSMSlot.cs`, current content, read in full above) + lock precedent from `Runtime/DSMWatcher.cs`

**Imports pattern** (`Runtime/DSMSlot.cs` lines 1-10, keep as-is, add nothing new — no new namespaces needed for `lock`/`SemaphoreSlim`, both are `System`/`System.Threading`, already partially imported):
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
```
`SemaphoreSlim` lives in `System.Threading` — already imported via the existing `CancellationTokenSource` usage; no new `using` required.

**Lock precedent to copy (D-05 sync-only lock)** — `Runtime/DSMWatcher.cs` lines 10-11, 42-52:
```csharp
private readonly object _lock = new();
...
public void Notify(string key, object value)
{
    List<Channel<object>>? snapshot;
    lock (_lock)
    {
        if (!_channels.TryGetValue(key, out var channels)) return;
        snapshot = new List<Channel<object>>(channels);
    }
    foreach (var channel in snapshot)
        channel.Writer.TryWrite(value);
}
```
Apply the same shape to `DSMSlot`: add `private readonly object _dataLock = new();`, wrap each `_data[...]`/`_data.TryGetValue`/`_data.ContainsKey`/`_data.Remove`/`_data.Clear` access in `Set`/`Get`/`Has`/`Delete`/`Clear` in `lock (_dataLock) { ... }`. Keep critical sections short and synchronous — do not call `_watcher.Notify()` or `ScheduleSave()` inside the lock (mirrors `DSMWatcher.Notify()`'s snapshot-then-release-then-iterate shape).

**Current `Set`/`Get`/`Has`/`Delete` to be wrapped** (`Runtime/DSMSlot.cs` lines 32-50):
```csharp
public void Set<T>(string key, T value) where T : notnull
{
    _data[key] = JToken.FromObject(value, _serializer.JsonSerializer);
    _watcher.Notify(key, value);
    if (_config.AutoSave)
        ScheduleSave();
}

public T Get<T>(string key, T defaultValue)
{
    if (!_data.TryGetValue(key, out var token)) return defaultValue;
    return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
}

public bool Has(string key) => _data.ContainsKey(key);

public void Delete(string key) => _data.Remove(key);

public void Clear() => _data.Clear();
```
Target shape (per D-05): dictionary read/write goes inside `lock (_dataLock)`; `_watcher.Notify`/`ScheduleSave` calls stay outside the lock, called after it's released, same statement-ordering style already used in this file (sequential top-to-bottom, no nesting).

**I/O gate pattern (D-05 SemaphoreSlim for await-spanning sections)** — new primitive, no existing in-repo `SemaphoreSlim` precedent, so follow the `try/finally` shape already established for cancellation cleanup at `Runtime/DSMSlot.cs` lines 151-156 (`CancelDebounce()`), applying the same "acquire → try → release in finally" discipline:
```csharp
private readonly SemaphoreSlim _ioGate = new(1, 1);

public async UniTask SaveAsync()
{
    await _ioGate.WaitAsync();
    try
    {
        CancelDebounce();
        // ... existing serialize + temp-write-then-rename body ...
    }
    finally
    {
        _ioGate.Release();
    }
}
```
Per PITFALLS.md Pitfall 5: always release in `finally`; never let an exception mid-`Save`/`Load` leave the gate permanently held.

**Atomic write-temp-then-rename (CONC-03)** — replace direct `File.WriteAllBytes`/`File.WriteAllText` calls at `Runtime/DSMSlot.cs` lines 58-61 and 86-89 with a temp-path write + `File.Replace`/move pattern. Current code to replace:
```csharp
if (_config.Encrypt)
    File.WriteAllBytes(path, DSMEncryptor.Encrypt(json, _config.EncryptionKey));
else
    File.WriteAllText(path, json);
```
Target shape: write to `path + ".tmp"`, then `File.Move(tmpPath, path, overwrite: true)` (or `File.Replace` if a backup file path is also desired) — same for the async `SaveAsync()` counterpart at lines 86-89. Keep the encrypt/plain branching structure identical (existing convention), only the destination-write mechanics change.

**ScheduleSave debounce rewrite (D-01)** — current CTS-cancel-and-recreate pattern at `Runtime/DSMSlot.cs` lines 136-156:
```csharp
private void ScheduleSave()
{
    // Reset the debounce timer on every Set() call
    _debounceCts?.Cancel();
    _debounceCts?.Dispose();
    _debounceCts = new CancellationTokenSource();
    DebouncedSaveAsync(_debounceCts.Token).Forget();
}

private async UniTaskVoid DebouncedSaveAsync(CancellationToken token)
{
    await UniTask.Delay(TimeSpan.FromSeconds(_config.AutoSaveDebounce), cancellationToken: token);
    await SaveAsync();
}

private void CancelDebounce()
{
    _debounceCts?.Cancel();
    _debounceCts?.Dispose();
    _debounceCts = null;
}
```
Replace with a single long-lived loop tracking "last requested save time" (D-01) — no per-`Set()` CTS churn. Keep method names/signatures stable where possible (`ScheduleSave()` called from `Set()`, still fire-and-forget via `UniTaskVoid`/`.Forget()` — matches the existing async-void convention already used here) so the calling code in `Set()` (line 37) needs zero changes.

**Malformed-JSON fallback (D-02)** — current `Load()`/`LoadAsync()` at lines 64-78 and 92-113 call `_serializer.Deserialize(json)` with no try/catch. Follow the exact silent-fallback-with-warning convention already used in `SeedDefaults()` (lines 123-134):
```csharp
private void SeedDefaults()
{
    _data = new Dictionary<string, JToken>();
    if (_constantType == null) return;
    foreach (var field in _constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var value = field.GetValue(null);
        if (value == null) continue;
        try { _data[field.Name] = JToken.FromObject(value, _serializer.JsonSerializer); }
        catch (Exception ex) { Debug.LogWarning($"DSM: Could not seed default for '{field.Name}': {ex.Message}"); }
    }
}
```
Apply the same `try { ... } catch (Exception ex) { Debug.LogWarning($"DSM: ..."); }` shape around the `_serializer.Deserialize(json)` call in both `Load()` and `LoadAsync()`; on catch, call `SeedDefaults()` instead of leaving `_data` in a partial/null state. Message must include slot name (`_slotName`) and `ex.Message` per D-02 and the codebase's `"DSM: {context}: {ex.Message}"` logging convention (CONVENTIONS.md "Message Format").

**Error handling pattern (existing convention, CONVENTIONS.md lines 130-148):**
```csharp
try { ... }
catch (System.Reflection.ReflectionTypeLoadException) { }
catch (Exception ex) { Debug.LogWarning($"DSM: {ex.Message}"); }
```
Reuse this exact `Debug.LogWarning($"DSM: ...")` prefix-with-context style for every new catch block added in this phase (JSON parse failure, slot-name rejection logging if applicable).

---

### `Runtime/DSMSlotManager.cs` (CHANGED — service, CRUD)

**Analog:** itself (current content above) + lock precedent from `Runtime/DSMWatcher.cs` lines 10-11, 56-73

**Current `_slots` access to be wrapped (coarse lock, D-04)** — `Runtime/DSMSlotManager.cs` lines 10, 65-71:
```csharp
private readonly Dictionary<string, DSMSlot> _slots = new();
...
private DSMSlot GetOrCreateSlot(string name)
{
    if (_slots.TryGetValue(name, out var slot)) return slot;
    slot = new DSMSlot(name, _config, _serializer, SaveDirectory, ResolveConstantType());
    _slots[name] = slot;
    return slot;
}
```
And `DeleteSlot` (lines 35-43):
```csharp
public void DeleteSlot(string name)
{
    _slots.Remove(name);
    var dir = SaveDirectory;
    var jsonPath = Path.Combine(dir, $"{name}.json");
    var encPath = Path.Combine(dir, $"{name}.enc");
    if (File.Exists(jsonPath)) File.Delete(jsonPath);
    if (File.Exists(encPath)) File.Delete(encPath);
}
```
Target shape (D-04 — separate coarse lock scoped only to add/remove-slot ops, mirroring `DSMWatcher.Register`/`Unregister` lock shape at lines 54-73):
```csharp
private readonly object _slotsLock = new();

private DSMSlot GetOrCreateSlot(string name)
{
    lock (_slotsLock)
    {
        if (_slots.TryGetValue(name, out var slot)) return slot;
        slot = new DSMSlot(name, _config, _serializer, SaveDirectory, ResolveConstantType());
        _slots[name] = slot;
        return slot;
    }
}
```
`DeleteSlot`'s `_slots.Remove(name)` line goes inside the same `lock (_slotsLock) { ... }`; keep file-delete I/O outside the lock (mirrors DSMSlot's "keep the dictionary lock short, I/O separate" principle from D-05/PITFALLS Performance Traps table — locking file I/O causes main-thread stalls).

**Slot-name validation entry point (D-03)** — call `DSMSlotNameValidator.Validate(name)` (new utility, see below) at the top of `GetOrCreateSlot()` and `UseSlot()` (line 27-31), before any dictionary/file-path work. Follow the existing "reject via throw, don't silently coerce" style already present in `DSMRuntimePanel.cs` line 32 (`throw new InvalidOperationException(...)`) — no new exception-handling idiom needed, this codebase already throws `InvalidOperationException` for invalid-input-at-the-boundary cases.

**Error handling pattern reused from `ResolveConstantType()`** (lines 73-90, unchanged, just for reference — no modification needed here, cited so `DSMSlotNameValidator` and any related logging matches this file's existing `Debug.LogWarning($"DSM: {ex.Message}")` convention).

---

### `Runtime/DSMSlotNameValidator.cs` (NEW — utility, transform)

**Analog:** `Runtime/DSMPaths.cs` (pure static utility, no Unity-engine-object dependency, consumed by both `DSMSlot`/`DSMSlotManager`) — read via STRUCTURE.md; same tier as `DSMSerializer`/`DSMEncryptor`.

**Shape to follow** (static utility class, no MonoBehaviour/ScriptableObject, matches `DSMEncryptor`'s all-static, `sealed`-free utility style referenced in ARCHITECTURE.md "DSMEncryptor (Static Utility)"):
```csharp
#nullable enable

using System;
using System.Text.RegularExpressions;

public static class DSMSlotNameValidator
{
    private static readonly Regex ValidPattern = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9",
    };

    public static void Validate(string name)
    {
        if (string.IsNullOrEmpty(name) || !ValidPattern.IsMatch(name) || name.Contains(".."))
            throw new ArgumentException($"DSM: Invalid slot name '{name}'.", nameof(name));

        foreach (var reserved in ReservedNames)
            if (string.Equals(name, reserved, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"DSM: Slot name '{name}' is a reserved device name.", nameof(name));
    }
}
```
Exact rule set per D-03/CONCERNS.md: reject anything outside alphanumeric + `_`/`-`; the regex already excludes `/`, `\`, and `..` implicitly (none of those chars are in the allowed set) — no separate check needed beyond confirming the regex is anchored (`^...$`). Naming convention: PascalCase class, matches `DSMPaths`/`DSMSerializer` naming (`DSM{Noun}` prefix per STRUCTURE.md "Naming Conventions").

---

### `Runtime/DSMRuntimePanel.cs` (CHANGED — BUGS-04 widget-missing warning)

**Analog:** itself (current content above) + `Runtime/Widgets/IntWidget.cs` missing-component warning convention (cited in ARCHITECTURE.md/CONVENTIONS.md, lines 16-20 per CONVENTIONS.md excerpt):
```csharp
if (_label == null || _input == null)
{
    Debug.LogError($"IntWidget on '{gameObject.name}': _label or _input is not assigned.", this);
    return;
}
```

**Current silent-fallback line to fix** — `Runtime/DSMRuntimePanel.cs` line 44:
```csharp
go.GetComponent<IDSMWidget>()?.Setup(entry.Key, entry.Type, label, slot);
```
Target shape (apply the same `Debug.LogError($"{Type} on '{gameObject.name}': ...", this)` convention, using the instantiated widget's own `gameObject.name` as context, matching `IntWidget`'s message format exactly):
```csharp
var widgetComponent = go.GetComponent<IDSMWidget>();
if (widgetComponent == null)
{
    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': widget prefab for '{entry.Key}' has no IDSMWidget component.", this);
    continue;
}
widgetComponent.Setup(entry.Key, entry.Type, label, slot);
```

---

### `Tests/Editor/DMS.Tests.Editor.asmdef` (NEW — config)

**Analog:** `Editor/DSM.Editor.asmdef` (existing asmdef, same package, read in full above)
```json
{
    "name": "DSM.Editor",
    "rootNamespace": "",
    "references": [
        "GUID:5c67e4f53d76c46cbabd0a518ee024c0"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
Target shape per TESTING.md's recommended structure and D-06 (EditMode-only, both asmdefs use `includePlatforms: ["Editor"]`):
```json
{
    "name": "DMS.Tests.Editor",
    "rootNamespace": "",
    "references": [
        "GUID:<DMS.Runtime's GUID — resolve by reading Runtime/DMS.Runtime.asmdef.meta>",
        "GUID:<DSM.Editor's GUID — resolve by reading Editor/DSM.Editor.asmdef.meta, if editor-only APIs are needed>"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": true,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```
Note: `overrideReferences: true` + `precompiledReferences: ["nunit.framework.dll"]` is the standard Unity Test Framework asmdef addition not present in either existing asmdef — required because this is the package's first test assembly (TESTING.md confirms zero existing test asmdefs).

---

### `Tests/Runtime/DMS.Tests.Runtime.asmdef` (NEW — config)

**Analog:** `Runtime/DMS.Runtime.asmdef` (existing asmdef, read in full above)
```json
{
    "name": "DMS.Runtime",
    "rootNamespace": "",
    "references": [
        "GUID:f51ebe6a0ceec4240a699833d6309b23",
        "GUID:5c01796d064528144a599661eaab93a6",
        "GUID:6055be8ebefd69e48b49212b09b47b2f"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
Per D-06: despite the name "Tests/Runtime", this asmdef still sets `includePlatforms: ["Editor"]` (runs in Edit Mode, not Play Mode — D-06 is explicit that this is not the same thing as PlayMode execution). Target shape:
```json
{
    "name": "DMS.Tests.Runtime",
    "rootNamespace": "",
    "references": [
        "GUID:<DMS.Runtime's GUID>"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": true,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

---

### `Tests/Editor/DSMSlotConcurrencyTests.cs` (NEW — test, request-response)

**No direct in-repo analog** (TESTING.md confirms zero existing test files) — use `.planning/codebase/TESTING.md`'s already-sketched recommended pattern verbatim as the template.

**Structure to copy** (TESTING.md lines 52-108, adapted to the actual `DSMSlot` constructor signature read above: `DSMSlot(string slotName, DSMConfig config, DSMSerializer serializer, string saveDirectory, Type? constantType)`):
```csharp
#nullable enable

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class DSMSlotConcurrencyTests
{
    private DSMConfig _config = null!;
    private DSMSerializer _serializer = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<DSMConfig>();
        _serializer = new DSMSerializer();
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DSM_Test_{Guid.NewGuid()}");
        System.IO.Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task ConcurrentSet_WithDistinctKeys_AllKeysPresentWithCorrectValues()
    {
        // Arrange — per PITFALLS.md Pitfall 6: assert final-state correctness, not just "no exception"
        var slot = new DSMSlot("test", _config, _serializer, _tempDir, null);
        const int count = 100;

        // Act
        await Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(() => slot.Set($"key{i}", i))));

        // Assert — every key present with its correct value, not just "didn't throw"
        for (var i = 0; i < count; i++)
            Assert.That(slot.Get($"key{i}", -1), Is.EqualTo(i));
    }
}
```
Per Pitfall 13/CONVENTIONS.md async patterns: use `async Task` `[Test]` methods (NUnit's native support), never `.GetAwaiter().GetResult()`/`.Result`/`.Wait()` blocking calls. Per D-06: no `[UnityTest]`/`IEnumerator` needed since `DSMSlot` has no MonoBehaviour dependency.

---

### `Tests/Editor/DSMSlotNameValidatorTests.cs` / `DSMSlotLoadRobustnessTests.cs` (NEW — test, request-response)

**No direct in-repo analog** — use TESTING.md's "Error Testing Pattern" (lines 311-343) as the template, adapted to the new `DSMSlotNameValidator` and `DSMSlot.Load()`'s malformed-JSON fallback:
```csharp
[Test]
public void Validate_WithPathTraversalSegment_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => DSMSlotNameValidator.Validate("../evil"));
}

[Test]
public void Validate_WithReservedDeviceName_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => DSMSlotNameValidator.Validate("CON"));
}

[Test]
public void Load_WithMalformedJsonFile_FallsBackToDefaultsWithoutThrowing()
{
    // Arrange: write corrupt JSON to the slot's expected load path, then Load()
    // Assert: Assert.DoesNotThrow(() => slot.Load()); and Assert.That(slot.Get("hp", 50), Is.EqualTo(50));
}
```

## Shared Patterns

### Lock-around-synchronous-state (D-05, sync half)
**Source:** `Runtime/DSMWatcher.cs` lines 10-11, 42-52 (the only existing thread-safety precedent in this codebase)
**Apply to:** `DSMSlot._data` access in `Set`/`Get`/`Has`/`Delete`/`Clear`; `DSMSlotManager._slots` access in `GetOrCreateSlot`/`DeleteSlot`
```csharp
private readonly object _lock = new();
lock (_lock)
{
    // short, synchronous critical section only — no await, no I/O
}
```

### Silent-fallback-with-warning (D-02, existing established convention)
**Source:** `Runtime/DSMSlot.cs` `SeedDefaults()` lines 123-134
**Apply to:** `DSMSlot.Load()`/`LoadAsync()` malformed-JSON handling
```csharp
try { /* risky operation */ }
catch (Exception ex) { Debug.LogWarning($"DSM: {contextMessage}: {ex.Message}"); /* fall back to safe default */ }
```

### Throw-at-the-boundary for invalid input (existing convention)
**Source:** `Runtime/DSMRuntimePanel.cs` line 32 (`throw new InvalidOperationException($"DSMRuntimePanel: slot '{_slotName}' not found")`)
**Apply to:** `DSMSlotNameValidator.Validate()` — throw `ArgumentException` synchronously at the call boundary (`DSMSlotManager.GetOrCreateSlot`/`UseSlot`), do not silently coerce/sanitize.

### Missing-component warning (BUGS-04, existing established convention)
**Source:** `Runtime/Widgets/IntWidget.cs` lines 16-20 (per CONVENTIONS.md excerpt) — `Debug.LogError($"{Type} on '{gameObject.name}': {field} is not assigned.", this)`
**Apply to:** `DSMRuntimePanel.BuildWidgets()` widget-missing-`IDSMWidget`-component case

### Async-spanning I/O gate (D-05, async half)
**Source:** No existing in-repo precedent (new pattern for this codebase) — informed by ARCHITECTURE.md Anti-Pattern 1 ("Locking across an `await`") and PITFALLS.md Pitfall 5
**Apply to:** `DSMSlot.Save()`/`SaveAsync()`/`Load()`/`LoadAsync()`
```csharp
private readonly SemaphoreSlim _ioGate = new(1, 1);
await _ioGate.WaitAsync();
try { /* file I/O */ }
finally { _ioGate.Release(); }
```

### NUnit test structure (TESTING.md sketch, first test in this codebase)
**Source:** `.planning/codebase/TESTING.md` lines 48-108 (`[TestFixture]`, `[SetUp]`/`[TearDown]` with temp directories, `async Task` test methods)
**Apply to:** All new files under `Tests/Editor/` and `Tests/Runtime/`

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Tests/Editor/DSMSlotConcurrencyTests.cs` | test | request-response | Zero existing test files in the codebase (TESTING.md confirms). Use TESTING.md's recommended NUnit/async-Task sketch as the template instead of an in-repo analog. |
| `Tests/Editor/DSMSlotNameValidatorTests.cs`, `DSMSlotLoadRobustnessTests.cs` | test | request-response | Same as above. |
| Async I/O gate (`SemaphoreSlim` usage inside `DSMSlot`) | (embedded in DSMSlot, not a separate file) | file-I/O | No existing `SemaphoreSlim` usage anywhere in the codebase — first introduction of this primitive. Follow ARCHITECTURE.md Anti-Pattern 1 and PITFALLS.md Pitfall 5 guidance directly (`WaitAsync()`/`Release()` in `finally`) rather than an in-repo analog. |

## Metadata

**Analog search scope:** `Runtime/*.cs`, `Editor/*.asmdef`, `Runtime/*.asmdef`, `.planning/codebase/*.md` (ARCHITECTURE, CONVENTIONS, STRUCTURE, TESTING), `.planning/research/*.md` (SUMMARY, ARCHITECTURE, PITFALLS)
**Files scanned:** `Runtime/DSMSlot.cs`, `Runtime/DSMSlotManager.cs`, `Runtime/DSMWatcher.cs`, `Runtime/DSMConfig.cs`, `Runtime/DSMRuntimePanel.cs`, `Runtime/DMS.Runtime.asmdef`, `Editor/DSM.Editor.asmdef`
**Pattern extraction date:** 2026-07-08

---
*Phase: 1-Foundation — Thread-Safety, Robustness & Test Infrastructure*
*Patterns mapped: 2026-07-08*
