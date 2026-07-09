#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class DSMSlotConcurrencyTests
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

    [Test]
    public void SetSaveLoad_Roundtrip_PersistsValue()
    {
        // Arrange
        var writerSlot = new DSMSlot("roundtrip", _config, _serializer, _tempDir, null);
        writerSlot.Set("hp", 42);

        // Act
        writerSlot.Save();
        var readerSlot = new DSMSlot("roundtrip", _config, _serializer, _tempDir, null);
        readerSlot.Load();

        // Assert — real disk write + read, proving the persistence path end-to-end
        Assert.That(readerSlot.Get("hp", 0), Is.EqualTo(42));
    }
}
