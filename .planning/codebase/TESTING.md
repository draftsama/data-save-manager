# Testing Patterns

**Analysis Date:** 2026-07-08

## Test Framework

**Current Status:**
- **No testing framework detected** — no NUnit, Jest, Vitest, or other test runner configured
- No test assembly definition file (no `*.Tests.asmdef` found)
- No test dependencies in `package.json`
- No test files in codebase (no `*.Test.cs` or `*.Spec.cs` files)

**Recommended Framework (if tests are added):**
- **Unity Test Framework (UTF)** — standard for Unity projects, natively supported
  - Runner: `Unity.TestFramework` package
  - Assertion: Built-in assertions or NSubstitute for mocking
  - Execution: Via Unity Test Runner window or CLI with `-runTests`

**Alternative (lighter for this package):**
- **NUnit** — lightweight, widely supported in C#
  - Runner: Unity's built-in support via `.Tests` assembly definitions
  - CLI: `dotnet test`

## Test File Organization

**Recommended Structure (not currently implemented):**

```
Assets/DataSaveManager/
├── Tests/                           # New test directory
│   ├── DMS.Tests.asmdef            # Test assembly definition
│   ├── Runtime/
│   │   ├── DSMSlotTests.cs         # Tests for DSMSlot
│   │   ├── DSMSerializerTests.cs   # Tests for DSMSerializer
│   │   ├── DSMEncryptorTests.cs    # Tests for DSMEncryptor
│   │   └── DSMWatcherTests.cs      # Tests for DSMWatcher
│   └── Editor/
│       ├── DSM.Editor.Tests.asmdef
│       ├── DSMCodeGeneratorTests.cs
│       └── DSMManagerWindowTests.cs
```

**Naming Pattern (recommended):**
- Test files: `[ComponentUnderTest]Tests.cs`
- Test classes: `[ComponentUnderTest]Tests`
- Test methods: `Test[Scenario]_[Expected]` or `When[Condition]_Then[Behavior]`

## Test Structure (Recommended Pattern)

Based on codebase style, tests should follow this structure:

```csharp
#nullable enable

using NUnit.Framework;
using System;

[TestFixture]
public class DSMSlotTests
{
    private DSMSlot? _slot;
    private DSMConfig? _config;
    private DSMSerializer? _serializer;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<DSMConfig>();
        _serializer = new DSMSerializer();
        _slot = new DSMSlot("test", _config, _serializer, tempDir, null);
    }

    [TearDown]
    public void TearDown()
    {
        _slot = null;
        // Cleanup temp files
    }

    [Test]
    public void Set_WhenCalled_UpdatesInMemoryData()
    {
        // Arrange
        var key = "testKey";
        var value = 42;

        // Act
        _slot.Set(key, value);

        // Assert
        Assert.That(_slot.Get(key, 0), Is.EqualTo(42));
    }

    [Test]
    public void Get_WhenKeyMissing_ReturnsDefaultValue()
    {
        // Arrange
        var missingKey = "nonexistent";
        var defaultValue = 100;

        // Act
        var result = _slot.Get(missingKey, defaultValue);

        // Assert
        Assert.That(result, Is.EqualTo(defaultValue));
    }
}
```

## Mocking Strategy (Recommended)

**What to Mock:**
- **File I/O:** Use temporary directories and cleanup in `[TearDown]`
- **Resources:** Use mock configs or create test assets programmatically
- **Async operations:** Mock UniTask delays with test-mode delays or direct synchronous calls

**What NOT to Mock:**
- Core logic (Set, Get, Has, Delete) — test real behavior
- Serialization — test with actual JSON serialization/deserialization
- Encryption/Decryption — test with real crypto (no mocking of security)

**Recommended Mocking Library:**
- **NSubstitute** — simple, clean syntax; good for Unity
- **Moq** — alternative, more verbose but powerful
- **Custom fakes** — for simple cases (e.g., fake DSMConfig)

**Example (NSubstitute-style, not yet implemented):**
```csharp
[Test]
public void UseSlot_WhenCalled_CallsLoadOnNewSlot()
{
    // Setup
    var mockSlot = Substitute.For<DSMSlot>(...);
    var manager = new DSMSlotManager(_config);

    // Act
    manager.UseSlot("newSlot");

    // Assert (would check Load was called)
}
```

## Fixtures and Test Data

**Recommended Patterns (not yet implemented):**

**Static Fixture Builder:**
```csharp
public static class DSMTestFixtures
{
    public static DSMConfig CreateTestConfig(
        bool autoSave = true,
        float autoSaveDebounce = 0.5f,
        bool encrypt = false)
    {
        var config = ScriptableObject.CreateInstance<DSMConfig>();
        // Set properties via reflection or setter methods
        return config;
    }

    public static Dictionary<string, JToken> CreateTestData()
    {
        return new Dictionary<string, JToken>
        {
            { "hp", JToken.FromObject(100) },
            { "speed", JToken.FromObject(3.5f) },
            { "name", JToken.FromObject("Hero") }
        };
    }
}
```

**Temporary Directory Fixture:**
```csharp
[SetUp]
public void SetUp()
{
    _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DSM_Test_{Guid.NewGuid()}");
    System.IO.Directory.CreateDirectory(_tempDir);
}

[TearDown]
public void TearDown()
{
    if (Directory.Exists(_tempDir))
        Directory.Delete(_tempDir, recursive: true);
}
```

## Coverage

**Current Status:**
- **Zero coverage** — no tests implemented

**Recommended Targets (if implementing tests):**
- `DSMSlot.cs` — Core get/set/save/load (High priority)
  - Set/Get/Has/Delete operations
  - Save/Load synchronous and async
  - Debounced save behavior
  - Default seeding from reflection
- `DSMSerializer.cs` — Serialization roundtrip (High priority)
  - Serialize and deserialize JSON
  - Handle all supported types
- `DSMEncryptor.cs` — Encryption/Decryption (High priority)
  - Encryption with random salt
  - Decryption of encrypted data
  - Format verification (IV + salt + ciphertext)
- `DSMWatcher.cs` — Reactive notifications (Medium priority)
  - Emit current value on subscribe
  - Emit new values on Notify
  - Cleanup on cancellation
- `DSMSlotManager.cs` — Slot lifecycle (Medium priority)
  - GetOrCreateSlot caching
  - DeleteSlot cleanup
  - GetAllSlots enumeration
- `DSMCodeGenerator.cs` — Code generation (Low priority)
  - Generate correct C# syntax
  - Handle type conversions
  - Escape strings correctly

## Test Types

### Unit Tests (Recommended)

**Scope:**
- Individual methods in isolation
- Mock external dependencies (File I/O, Resources loading)
- Use temporary directories for file system tests

**Example Areas:**
- `DSMSlot.Get<T>()` — value retrieval with defaults
- `DSMSerializer.Serialize()` — JSON generation
- `DSMEncryptor.Encrypt/Decrypt()` — symmetric encryption roundtrip
- `DSMWatcher.Notify()` — channel writes

### Integration Tests (Optional)

**Scope:**
- Multi-component interactions
- Real file I/O without mocking
- Real JSON serialization/deserialization
- Encryption with seeded data

**Example Scenarios:**
- Set a value in a slot, save to disk, load in new slot, verify value matches
- Encrypt a slot, decrypt the file bytes, verify decryption matches original JSON
- Watch a key, set value, verify async stream receives the new value

### Editor Tests (Specific to DSM Editor components)

**Scope:**
- `DSMCodeGenerator.Generate()` — verify generated `.cs` file content
- `DSMManagerWindow` — UI state transitions (requires EditorTest framework)
- `DSMConstantSyncer` — post-compile refresh behavior

**Note:** Editor tests require Unity Test Framework's `UnityEditor.TestTools.TestsAttribute` and are slower.

## Async Testing Pattern

**UniTask Async Tests (recommended if implemented):**

```csharp
[Test]
public async UniTask SaveAsync_WhenCalled_WritesFileToRealDisk()
{
    // Arrange
    var tempDir = CreateTempDirectory();
    var slot = new DSMSlot("test", _config, _serializer, tempDir, null);
    slot.Set("key", "value");

    // Act
    await slot.SaveAsync();

    // Assert
    var savedPath = Path.Combine(tempDir, "test.json");
    Assert.That(File.Exists(savedPath), Is.True);
    var json = File.ReadAllText(savedPath);
    Assert.That(json, Does.Contain("key"));
}
```

**Cancellation Token Testing:**

```csharp
[Test]
public async UniTask WatchAsync_WhenCancelled_StopsEmitting()
{
    // Arrange
    var cts = new CancellationTokenSource();
    var watcher = new DSMWatcher();
    var receivedValues = new List<int>();

    // Act
    var task = Task.Run(async () =>
    {
        await foreach (var value in watcher.Watch<int>("key", () => (false, 0)).WithCancellation(cts.Token))
        {
            receivedValues.Add(value);
        }
    });

    await UniTask.Delay(100);
    cts.Cancel();
    await task;

    // Assert
    Assert.That(receivedValues, Is.Empty);
}
```

## Error Testing Pattern

**Exception Verification (recommended):**

```csharp
[Test]
public void Decrypt_WithInvalidKey_ThrowsCryptographicException()
{
    // Arrange
    var encrypted = DSMEncryptor.Encrypt("test data", "correct-key");
    var wrongKey = "wrong-key";

    // Act & Assert
    Assert.Throws<CryptographicException>(() =>
    {
        DSMEncryptor.Decrypt(encrypted, wrongKey);
    });
}

[Test]
public void Get_WithInvalidJson_ReturnsDefaultValue()
{
    // Arrange
    var slot = new DSMSlot("test", _config, _serializer, tempDir, null);
    // Manually corrupt _data to simulate invalid JSON

    // Act
    var result = slot.Get<int>("key", 99);

    // Assert
    Assert.That(result, Is.EqualTo(99));
}
```

## Test Organization by Layer

**Recommended assembly definitions:**

```json
// DMS.Tests.asmdef
{
    "name": "DMS.Tests",
    "references": ["DMS.Runtime"],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

```json
// DSM.Editor.Tests.asmdef
{
    "name": "DSM.Editor.Tests",
    "references": ["DSM.Runtime", "DSM.Editor"],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

## File-Level Testing Guidance

| Component | Test Approach | Priority |
|-----------|---------------|----------|
| `DSM.cs` | Mock DSMSlotManager, verify delegation | Low |
| `DSMSlot.cs` | Temp directories, real JSON/encryption, verify state | **High** |
| `DSMSlotManager.cs` | Test caching, slot creation, deletion | **High** |
| `DSMSerializer.cs` | Roundtrip serialization for all types | **High** |
| `DSMEncryptor.cs` | Encrypt/decrypt roundtrip, salt randomness | **High** |
| `DSMWatcher.cs` | Async stream subscription, notification delivery | Medium |
| `DSMConfig.cs` | Property access, default values, serialization | Low |
| `IDSMWidget.cs` | No tests needed (interface contract) | N/A |
| `DSMCodeGenerator.cs` | Generated file content verification | Medium |
| `DSMManagerWindow.cs` | EditorTest framework (UI integration) | Low |
| `Widget classes` | Mock IDSMWidget contract, verify UI binding | Low |
| `Converter classes` | Roundtrip JSON serialization per type | Medium |

## Common Testing Pitfalls to Avoid

1. **Not cleaning up temp files** — Leads to disk space issues and test flakiness
2. **Mocking serialization logic** — Defeats the purpose; test real JSON handling
3. **Hardcoding encryption keys in tests** — Use fixture builders with consistent test keys
4. **Ignoring async/await in tests** — Must properly await UniTask operations, use `[UnityTest]` attribute for async tests
5. **Not testing default seeding** — The reflection-based constant injection is fragile; needs dedicated tests
6. **Ignoring thread safety in DSMWatcher** — Must test concurrent Notify calls with snapshots

---

*Testing analysis: 2026-07-08*
