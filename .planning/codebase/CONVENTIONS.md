# Coding Conventions

**Analysis Date:** 2026-07-08

## Nullable Reference Types

**Standard Practice:**
- All files begin with `#nullable enable` (`DSM.cs`, `DSMSlot.cs`, `DSMEncryptor.cs`, etc.)
- Use `?` for nullable types explicitly
- Use null-coalescing operators `??` for defaults
- Use safe navigation `?.` for defensive null access
- Use pattern matching `if (value is T typedValue)` for type-safe null checks

**Example:**
```csharp
// From DSMSlot.cs
private Dictionary<string, JToken> _data = new();
public T Get<T>(string key, T defaultValue)
{
    if (!_data.TryGetValue(key, out var token)) return defaultValue;
    return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
}
```

**Example (safe navigation):**
```csharp
// From IntWidget.cs
if (_label == null || _input == null)
{
    Debug.LogError($"IntWidget on '{gameObject.name}': _label or _input is not assigned.", this);
    return;
}
```

## Naming Patterns

**Files:**
- PascalCase: `DSM.cs`, `DSMSlot.cs`, `DSMSlotManager.cs`, `IntWidget.cs`
- Abbreviated domain prefix: All classes start with `DSM` (DataSaveManager) or component-type suffix (e.g., `Widget`)

**Classes:**
- PascalCase: `DSMSlot`, `DSMWatcher`, `IntWidget`, `Vector3Converter`
- `sealed class` for concrete implementations (most classes are sealed: `DSMSlot`, `DSMConfig`, `IntWidget`)

**Private fields:**
- Prefix with `_`: `_slotName`, `_config`, `_data`, `_debounceCts`

**Static fields:**
- Prefix with `s_`: `s_manager` in `DSM.cs`, `s_constantType` in `DSMSlotManager.cs`
- Lazy-initialized static fields use `??=` pattern

**Static style caches:**
- Prefix with `s_`, initialize with `??=`: `s_headerLabel ??= new GUIStyle(...)` in `DSMManagerWindow.cs`

**Properties:**
- PascalCase: `Manager`, `ActiveSlot`, `SaveDirectory`
- Read-only properties with backing fields: `public bool AutoSave => _autoSave`

**Methods:**
- PascalCase: `UseSlot()`, `GetSlot()`, `DeleteSlot()`, `WatchAsync<T>()`
- Async suffix for async methods: `SaveAsync()`, `LoadAsync()`, `DebouncedSaveAsync()`
- Fire-and-forget async methods return `UniTaskVoid`: `DebouncedSaveAsync()` in `DSMSlot.cs`

**Method Parameters:**
- camelCase: `name`, `key`, `value`, `slotName`
- Descriptive names: `currentValueProvider` in `DSMWatcher.Watch<T>()`

**Generic Type Parameters:**
- Single letter: `<T>` for generic value types
- Used throughout: `Set<T>()`, `Get<T>()`, `WatchAsync<T>()`

**Constants:**
- ALL_CAPS: `KeySize`, `IvSize`, `SaltSize`, `Iterations` in `DSMEncryptor.cs`
- Private const strings for paths: `OutputPath` in `DSMCodeGenerator.cs`

**Entry keys (in DSMConstant):**
- PascalCase (convention enforced): `hp`, `speed`, `saveName`
- Lowercase keys trigger a warning from `DSMCodeGenerator` (line 30)

## Code Style

**Formatting:**
- No explicit code formatter configuration (`.editorconfig`, `.prettierrc`, etc.)
- Follows C# conventions: 4-space indentation, consistent with Visual Studio defaults
- Multi-line method chains use fluent style when appropriate

**Linting:**
- No explicit linter configuration detected
- Relies on Visual Studio/Rider implicit conventions

**Braces:**
- Allman-style braces for blocks: `{ }` on new lines
- Exception: Single-statement blocks are common: `if (condition) return;` (line 19 in `IntWidget.cs`)
- Expression bodies for simple properties: `public bool AutoSave => _autoSave;` (line 13 in `DSMConfig.cs`)

**Line Length:**
- No strict limit enforced; typical range 80-120 characters
- Long parameter lists use stack notation: `WritePropertyName("x"); writer.WriteValue(value.x);` (lines 13-14 in `Vector3Converter.cs`)

## Import Organization

**Order (examples from files):**
1. `using System;` and `System.*` namespaces first
2. `using UnityEngine;` and other third-party packages (Cysharp, Newtonsoft)
3. No `using` statements for local classes (root namespace)

**Example from DSMSlot.cs:**
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

**Editor Conditional Compilation:**
- Guard editor-only code with `#if UNITY_EDITOR` after nullable directive
- Example: `DSMManagerWindow.cs` line 2: `#if UNITY_EDITOR`

**Path Aliases:**
- Not used; root namespace is empty in asmdef files

## Error Handling

**Patterns:**

1. **Try-Catch for Defensive Code:**
   - Catch specific exceptions first, then general `Exception`
   - Log warnings for recoverable errors
   
   Example from `DSMSlotManager.cs` (lines 78-87):
   ```csharp
   try
   {
       foreach (var t in assembly.GetTypes())
       {
           if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
               return s_constantType = t;
       }
   }
   catch (System.Reflection.ReflectionTypeLoadException) { }
   catch (Exception ex) { Debug.LogWarning($"DSM: {ex.Message}"); }
   ```

2. **Try-Finally for Resource Cleanup:**
   - Used in async patterns (DSMWatcher.cs lines 27-38)
   - Paired with `using` statements for IDisposable resources
   
   Example from `DSMSlot.cs` (lines 87-89):
   ```csharp
   if (_config.Encrypt)
       await File.WriteAllBytesAsync(path, DSMEncryptor.Encrypt(json, ...));
   else
       await File.WriteAllTextAsync(path, json);
   ```

3. **Null Checks with Early Return:**
   - Defensive checks at method entry
   
   Example from `IntWidget.cs` (lines 16-20):
   ```csharp
   if (_label == null || _input == null)
   {
       Debug.LogError($"IntWidget on '{gameObject.name}': _label or _input is not assigned.", this);
       return;
   }
   ```

4. **Collections and Empty State:**
   - Return `Array.Empty<string>()` instead of `new string[0]` (line 48 in `DSMSlotManager.cs`)
   - Default values in parsing: `? 0f` for floats, `false` for bools (Vector3Converter.cs line 24)

**No Explicit Exception Throwing:**
- The codebase generally lets exceptions propagate naturally (e.g., file I/O errors)
- Only caught and logged when recovery is possible

## Logging

**Framework:**
- Uses `UnityEngine.Debug` exclusively
- Three methods: `Debug.Log()`, `Debug.LogWarning()`, `Debug.LogError()`

**Patterns:**

1. **Info Messages:**
   - `Debug.Log()` for generation success: `"DSM: Generated {OutputPath} with {entries.Count} entries."` (DSMCodeGenerator.cs line 45)

2. **Warnings:**
   - `Debug.LogWarning()` for naming convention violations: `"DSM: Key '{entry.Key}' does not follow PascalCase convention."` (DSMCodeGenerator.cs line 30)
   - For reflection errors: `Debug.LogWarning($"DSM: {ex.Message}")` (DSMSlotManager.cs line 87)
   - For seeding failures: `Debug.LogWarning($"DSM: Could not seed default for '{field.Name}': {ex.Message}")` (DSMSlot.cs line 132)

3. **Errors:**
   - `Debug.LogError()` for missing required components: `"IntWidget on '{gameObject.name}': _label or _input is not assigned."` (IntWidget.cs line 18)

**Message Format:**
- Prefix with domain: `"DSM: "` or component name
- Include context (gameObject.name, field.Name, key, etc.)
- Pass `this` as context for MonoBehaviour messages

## Comments

**When to Comment:**

1. **Algorithm Explanation:**
   - For non-obvious logic, especially encryption/hashing
   - Example from `DSMEncryptor.cs` (lines 14-15):
     ```csharp
     // File format: [16-byte IV][32-byte salt][ciphertext]
     // Salt is generated randomly per Encrypt() call — no fixed salt.
     ```

2. **Workflow / Intent Comments:**
   - Using `// ─────` separators for section grouping (DSMManagerWindow.cs lines 18, 48, 65)
   - Example: `// ── State ────────────────────────────────────────────────────────────────`
   - Marks major logical sections (State, Styles, Lifecycle)

3. **Inline Reset/Debounce Logic:**
   - For state management like cancellation (DSMSlot.cs line 138):
     ```csharp
     // Reset the debounce timer on every Set() call
     ```

**JSDoc / XML Documentation:**
- Used for **public API only** (DSM.cs, DSMSlot public methods)
- Three-tag pattern: `<summary>`, `<paramref name="...">`, `<see cref="...">`
- Example from DSM.cs (lines 12-13):
  ```csharp
  /// <summary>Overrides the auto-loaded DSMConfig. Call before any other DSM method to use a custom config.</summary>
  public static void Configure(DSMConfig config)
  ```

- **NOT used** for private methods, internal classes, or implementation details
- README.md serves as the primary user-facing documentation

## Function Design

**Size:**
- Small, focused functions (most <50 lines)
- Larger functions reserved for UI layout (DSMManagerWindow.cs with multiple drawing methods)

**Parameters:**
- Generic constraints when appropriate: `where T : notnull` in `Set<T>()` and `Get<T>()`
- Out parameters sparingly: `out byte[] iv` in `CreateAes()` (DSMEncryptor.cs)
- Action/Func delegates for callbacks: `Func<(bool exists, T value)>` in `Watch<T>()` (DSMWatcher.cs)

**Return Values:**
- Use tuples for multiple returns: `(bool exists, T value)` from `currentValueProvider` (DSMWatcher.cs line 20)
- Return defaults on missing data: `Get<T>()` returns parameter `defaultValue`
- Return empty collections: `Array.Empty<string>()` (DSMSlotManager.cs line 48)
- Return `IReadOnlyList<T>` for public collections: `ExposedEntries` (DSMConfig.cs line 49)

## Module Design

**Exports:**
- Public static facade: `DSM.cs` is the main API entry point
- `sealed class` for implementation details (almost all runtime classes)
- Public interfaces: `IDSMWidget.cs` for widget contract

**Barrel Files:**
- Not used; single-responsibility classes with no re-export pattern

**Organization:**
- Runtime directory contains: core logic, serialization, reactivity, widgets
- Editor directory contains: window UI, code generation, configuration editors
- Types subdirectory contains: helper data classes (Transform snapshots, Converters)
- Widgets subdirectory contains: MonoBehaviour components implementing IDSMWidget

## Async Patterns

**UniTask Usage:**
- All async operations use `UniTask` (not `System.Threading.Tasks.Task`)
- Fire-and-forget: `.Forget()` on `UniTaskVoid` methods (DSMSlot.cs line 142)
- Async iterables: `IUniTaskAsyncEnumerable<T>` for streaming (DSMWatcher.cs, DSMSlot.cs)
- Cancellation: `CancellationToken` as method parameter for async operations

**Example:**
```csharp
// From DSMSlot.cs line 142
DebouncedSaveAsync(_debounceCts.Token).Forget();
```

**Async Streaming Pattern (DSMWatcher.cs):**
```csharp
public IUniTaskAsyncEnumerable<T> Watch<T>(string key, Func<(bool exists, T value)> currentValueProvider)
{
    return UniTaskAsyncEnumerable.Create<T>(async (writer, token) =>
    {
        // ... implementation
    });
}
```

## Thread Safety

**Patterns:**

1. **Explicit Lock Statements:**
   - Protect shared collections (DSMWatcher.cs lines 45-49)
   - Example: `lock (_lock) { /* access _channels */ }`

2. **Snapshot Copies:**
   - Create local copies of shared state before iteration (DSMWatcher.cs line 48)
   ```csharp
   snapshot = new List<Channel<object>>(channels);
   ```

3. **Immutable Snapshots:**
   - Use `IReadOnlyList<T>` for public collections (DSMConfig.cs line 49)
   - Prevents external modification

## ScriptableObject Patterns

**Field Backing:**
- Private `[SerializeField]` backing field with public read-only property (DSMConfig.cs)
- Example:
  ```csharp
  [SerializeField, FormerlySerializedAs("<AutoSave>k__BackingField")]
  private bool _autoSave = true;
  public bool AutoSave => _autoSave;
  ```

**FormerlySerializedAs:**
- Used extensively to track property refactorings (DSMConfig.cs lines 11, 15, 19, 28, 32, 36)
- Ensures save-file compatibility across versions

**Non-Serialized Secret Fields:**
- Use `[field: NonSerialized]` for properties that must not be persisted (EncryptionKey in DSMConfig.cs line 24)

**Nested Serializable Classes:**
- Sealed inner types for configuration structures (DSMConfig.cs lines 40-46):
  ```csharp
  [Serializable]
  public sealed class ExposedEntry
  ```

---

*Convention analysis: 2026-07-08*
