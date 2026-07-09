#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class DSMSlotAtomicSaveTests
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

    [Test]
    public async Task SaveAsync_WritesCompleteFile_NoTempLeftOver()
    {
        // Arrange
        var slot = new DSMSlot("atomic-async", _config, _serializer, _tempDir, null);
        slot.Set("hp", 42);

        // Act
        await slot.SaveAsync();

        // Assert — destination exists, parses as JSON, no sibling .tmp remains
        var destPath = Path.Combine(_tempDir, "atomic-async.json");
        Assert.That(File.Exists(destPath), Is.True, "destination file should exist after SaveAsync");
        Assert.DoesNotThrow(() => JObject.Parse(File.ReadAllText(destPath)));

        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty, "no .tmp file should remain after a successful save");
    }

    [Test]
    public void Save_IsAtomic_DestinationNeverPartial()
    {
        // Arrange — populate an existing destination file first
        var slot = new DSMSlot("atomic-sync", _config, _serializer, _tempDir, null);
        slot.Set("hp", 10);
        slot.Save();

        // Act — save again over the existing populated file with new data
        slot.Set("hp", 99);
        slot.Set("mp", 55);
        slot.Save();

        // Assert — destination always fully parseable (temp-then-replace, never truncated)
        var destPath = Path.Combine(_tempDir, "atomic-sync.json");
        var json = File.ReadAllText(destPath);
        var root = JObject.Parse(json);
        Assert.That((int)root["hp"]!, Is.EqualTo(99));
        Assert.That((int)root["mp"]!, Is.EqualTo(55));

        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty, "no .tmp file should remain after a successful save");
    }

    [Test]
    public async Task ConcurrentSetAndSaveAsync_FinalStateConsistent()
    {
        // Arrange
        var slot = new DSMSlot("atomic-concurrent", _config, _serializer, _tempDir, null);
        const int count = 50;

        // Act — interleave Set and SaveAsync calls
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(async () =>
            {
                slot.Set($"key{i}", i);
                await slot.SaveAsync();
            }));
        await Task.WhenAll(tasks);

        // Final settle save to guarantee a well-defined end state
        await slot.SaveAsync();

        // Assert — in-memory state and persisted file are mutually consistent
        for (var i = 0; i < count; i++)
            Assert.That(slot.Get($"key{i}", -1), Is.EqualTo(i));

        var destPath = Path.Combine(_tempDir, "atomic-concurrent.json");
        Assert.That(File.Exists(destPath), Is.True);
        var root = JObject.Parse(File.ReadAllText(destPath));
        for (var i = 0; i < count; i++)
            Assert.That((int)root[$"key{i}"]!, Is.EqualTo(i));

        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty, "no .tmp file should remain after the gate serializes concurrent saves");
    }
}
