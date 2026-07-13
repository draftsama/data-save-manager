#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class DSMSlotLoadRobustnessTests
{
    private DSMConfig _config = null!;
    private DSMSerializer _serializer = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _config = DSMTestConfig.Create();
        _serializer = new DSMSerializer();
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteMalformedJson(string slotName)
    {
        var path = Path.Combine(_tempDir, $"{slotName}.json");
        File.WriteAllText(path, "{ not valid json");
    }

    [Test]
    public void Load_WithMalformedJsonFile_FallsBackToDefaultsWithoutThrowing()
    {
        // Arrange
        WriteMalformedJson("corrupt-sync");
        var slot = new DSMSlot("corrupt-sync", _config, _serializer, _tempDir, null);
        LogAssert.ignoreFailingMessages = true;

        // Act & Assert — D-02: never throw, fall back to caller default
        Assert.DoesNotThrow(() => slot.Load());
        Assert.That(slot.Get("hp", 50), Is.EqualTo(50));

        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public async Task LoadAsync_WithMalformedJsonFile_FallsBackToDefaults()
    {
        // Arrange
        WriteMalformedJson("corrupt-async");
        var slot = new DSMSlot("corrupt-async", _config, _serializer, _tempDir, null);
        LogAssert.ignoreFailingMessages = true;

        // Act & Assert — await directly instead of Assert.DoesNotThrowAsync, which
        // blocks synchronously via NUnit's AsyncToSyncAdapter and deadlocks against
        // Unity's editor SynchronizationContext when the awaited continuation needs
        // to marshal back onto the (blocked) main thread.
        try
        {
            await slot.LoadAsync();
        }
        catch (Exception ex)
        {
            Assert.Fail($"LoadAsync threw: {ex}");
        }
        Assert.That(slot.Get("hp", 50), Is.EqualTo(50));

        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void Load_WithMalformedJsonFile_LogsWarningNamingSlotAndError()
    {
        // Arrange
        WriteMalformedJson("corrupt-warn");
        var slot = new DSMSlot("corrupt-warn", _config, _serializer, _tempDir, null);

        // Act & Assert — a single warning naming the slot is emitted
        LogAssert.Expect(LogType.Warning, new Regex("DSM: slot 'corrupt-warn' has malformed save data.*"));
        slot.Load();
    }

    [Test]
    public void Load_WithMalformedJsonFile_SeedsDSMConstantDefaults()
    {
        // Arrange — a slot with a real DSMConstant type present falls back to its seeded defaults
        WriteMalformedJson("corrupt-seeded");
        var slot = new DSMSlot("corrupt-seeded", _config, _serializer, _tempDir, typeof(DSMConstant));
        LogAssert.ignoreFailingMessages = true;

        // Act
        Assert.DoesNotThrow(() => slot.Load());

        // Assert — seeded from DSMConstant.testKey = 24 rather than the caller-supplied default
        Assert.That(slot.Get("testKey", -1), Is.EqualTo(24));

        LogAssert.ignoreFailingMessages = false;
    }
}
