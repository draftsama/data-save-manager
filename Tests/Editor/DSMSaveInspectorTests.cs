#nullable enable

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class DSMSaveInspectorTests
{
    private const string Key = "inspector-key-123";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_Inspector_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void TryReadOnDiskVersion_EnvelopeFile_ReturnsEnvelopeVersion()
    {
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var path = WriteEnvelope("versioned", 3);

        var read = DSMSaveInspector.TryReadOnDiskVersion(path, config, out var version);

        Assert.That(read, Is.True);
        Assert.That(version, Is.EqualTo(3));
    }

    [Test]
    public void TryReadOnDiskVersion_LegacyFlatFile_ReturnsLegacyVersion()
    {
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var path = Path.Combine(_tempDir, "legacy.json");
        File.WriteAllText(path, new JObject { ["hp"] = 10 }.ToString(), Encoding.UTF8);

        var read = DSMSaveInspector.TryReadOnDiskVersion(path, config, out var version);

        // A readable save with no envelope is a v1 save, not a failed read.
        Assert.That(read, Is.True);
        Assert.That(version, Is.EqualTo(DSMSaveEnvelope.LegacyVersion));
    }

    [Test]
    public void TryReadOnDiskVersion_MissingFile_ReturnsFalse()
    {
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var path = Path.Combine(_tempDir, "does-not-exist.json");

        var read = DSMSaveInspector.TryReadOnDiskVersion(path, config, out var version);

        Assert.That(read, Is.False);
        Assert.That(version, Is.EqualTo(DSMSaveEnvelope.LegacyVersion));
    }

    [Test]
    public void TryReadOnDiskVersion_CorruptFile_ReturnsFalseWithoutThrowing()
    {
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "{ this is not json", Encoding.UTF8);

        var read = false;
        Assert.DoesNotThrow(() => read = DSMSaveInspector.TryReadOnDiskVersion(path, config, out _));
        Assert.That(read, Is.False);
    }

    [Test]
    public void TryReadOnDiskVersion_EncryptedSlot_DecryptsAndReadsVersion()
    {
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: Key, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("encrypted");
        slot.Set("hp", 7);
        slot.Save();
        var path = Path.Combine(_tempDir, "encrypted.enc");
        Assert.That(File.Exists(path), Is.True, "encrypted save file must exist");

        var read = DSMSaveInspector.TryReadOnDiskVersion(path, config, out var version);

        Assert.That(read, Is.True);
        // Compared against this manager, not the DSM facade: touching DSM would build the
        // global manager against the project's real save directory.
        Assert.That(version, Is.EqualTo(manager.CurrentVersion));
    }

    [Test]
    public void TryReadOnDiskVersion_EncryptedSlotWithWrongKey_ReturnsFalse()
    {
        var writerConfig = DSMTestConfig.Create(encrypt: true, encryptionKey: Key, savePath: _tempDir);
        var manager = new DSMSlotManager(writerConfig);
        var slot = manager.GetSlot("encrypted-wrong-key");
        slot.Set("hp", 7);
        slot.Save();
        var path = Path.Combine(_tempDir, "encrypted-wrong-key.enc");
        var readerConfig = DSMTestConfig.Create(encrypt: true, encryptionKey: "another-key-456", savePath: _tempDir);

        var read = false;
        Assert.DoesNotThrow(() => read = DSMSaveInspector.TryReadOnDiskVersion(path, readerConfig, out _));
        Assert.That(read, Is.False);
    }

    [Test]
    public void TryReadOnDiskVersion_DoesNotMutateTheFile()
    {
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var path = WriteEnvelope("untouched", 2);
        var before = File.ReadAllBytes(path);
        var writtenAt = File.GetLastWriteTimeUtc(path);

        DSMSaveInspector.TryReadOnDiskVersion(path, config, out _);

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(writtenAt));
    }

    private string WriteEnvelope(string slot, int version)
    {
        var path = Path.Combine(_tempDir, $"{slot}.json");
        var envelope = DSMSaveEnvelope.Wrap(new JObject { ["hp"] = 10 }, version);
        File.WriteAllText(path, envelope.ToString(), Encoding.UTF8);
        return path;
    }
}
