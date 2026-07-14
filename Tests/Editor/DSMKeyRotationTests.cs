#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// Covers atomic key rotation (ENC-02): re-encrypt-all-then-commit-key-last staging,
/// pre-commit failure leaving every slot on the old key, journal-based recovery of an
/// interrupted commit, and the TEST-02 key-change-between-saves scenario.
/// </summary>
[TestFixture]
public class DSMKeyRotationTests
{
    private const string KeyA = "key-alpha-123";
    private const string KeyB = "key-bravo-456";
    private const string KeyC = "key-charlie-789";

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_RotationTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Rotate_ReencryptsAllSlots_ReadableWithNewKey()
    {
        // Arrange — 3 slots saved with key A
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        var names = new[] { "slot-a", "slot-b", "slot-c" };
        foreach (var name in names)
        {
            var slot = manager.GetSlot(name);
            slot.Set("value", name);
            await slot.SaveAsync();
        }

        // Act
        await manager.RotateEncryptionKeyAsync(KeyB);

        // Assert — every slot loads with key B and returns its original data
        foreach (var name in names)
        {
            var slot = manager.GetSlot(name);
            await slot.LoadAsync();
            Assert.That(slot.Get("value", ""), Is.EqualTo(name));
        }

        // Assert — the old key no longer decrypts the files on disk
        foreach (var name in names)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, $"{name}.enc"));
            Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(bytes, KeyA));
        }
    }

    [Test]
    public async Task Rotate_PreCommitFailure_LeavesAllSlotsOnOldKey()
    {
        // Arrange — 2 valid slots on key A
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        var validNames = new[] { "valid-1", "valid-2" };
        foreach (var name in validNames)
        {
            var slot = manager.GetSlot(name);
            slot.Set("value", name);
            await slot.SaveAsync();
        }

        // Plant a third .enc file encrypted with a DIFFERENT key (C) — cannot decrypt with key A
        var badBytes = DSMEncryptor.Encrypt("{}", KeyC);
        await File.WriteAllBytesAsync(Path.Combine(_tempDir, "corrupt-slot.enc"), badBytes);

        // Act / Assert — rotation throws
        Assert.ThrowsAsync<DSMEncryptionException>(async () => await manager.RotateEncryptionKeyAsync(KeyB));

        // Assert — config key unchanged
        Assert.That(config.EncryptionKey, Is.EqualTo(KeyA));

        // Assert — the 2 valid slots still decrypt with key A
        foreach (var name in validNames)
        {
            var slot = manager.GetSlot(name);
            await slot.LoadAsync();
            Assert.That(slot.Get("value", ""), Is.EqualTo(name));
        }

        // Assert — no leftover .tmp staged files
        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty, "no staged .tmp file should remain after a pre-commit failure");
    }

    [Test]
    public async Task Rotate_JournalRecovery_CompletesInterruptedCommit()
    {
        // Arrange — simulate a crash mid-commit by hand
        const string slotName = "recover-slot";
        var encPath = Path.Combine(_tempDir, $"{slotName}.enc");
        await File.WriteAllBytesAsync(encPath, DSMEncryptor.Encrypt("{\"value\":\"old\"}", KeyA));

        var tmpPath = encPath + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, DSMEncryptor.Encrypt("{\"value\":\"new\"}", KeyB));

        var journalPath = Path.Combine(_tempDir, DSMSlotManager.RotationJournalName);
        await File.WriteAllTextAsync(journalPath, slotName);

        // Act — construct a NEW DSMSlotManager whose config key is B
        var configB = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyB, savePath: _tempDir);
        var manager = new DSMSlotManager(configB);

        // Assert — recovery renamed the .tmp into place and removed the journal
        Assert.That(File.Exists(journalPath), Is.False, "journal file should be deleted after recovery");
        Assert.That(File.Exists(tmpPath), Is.False, "staged .tmp should be renamed away after recovery");

        var slot = manager.GetSlot(slotName);
        await slot.LoadAsync();
        Assert.That(slot.Get("value", ""), Is.EqualTo("new"));
    }

    [Test]
    public async Task RotateThenReload_KeyChangeBetweenSaves_DataSurvives()
    {
        // Arrange — save a slot with key A via the DSM facade
        var configA = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        DSM.Configure(configA);
        DSM.Set("score", 777);
        await DSM.SaveAsync();

        // Act — rotate to key B
        await DSM.RotateEncryptionKeyAsync(KeyB);

        // Construct a fresh manager (config key B) against the same dir
        var configB = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyB, savePath: _tempDir);
        DSM.Configure(configB);
        await DSM.LoadAsync();

        // Assert — original data survives under the new key
        Assert.That(DSM.Get("score", -1), Is.EqualTo(777));
    }

    [Test]
    public void Rotate_ToWeakKey_Rejected()
    {
        // Arrange
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        const string slotName = "weak-key-slot";
        var slot = manager.GetSlot(slotName);
        slot.Set("value", "unchanged");
        slot.Save();

        // Act / Assert — empty and too-short keys are rejected
        Assert.ThrowsAsync<ArgumentException>(async () => await manager.RotateEncryptionKeyAsync(""));
        Assert.ThrowsAsync<ArgumentException>(async () => await manager.RotateEncryptionKeyAsync("short"));

        // Assert — config key unchanged and no slot files modified
        Assert.That(config.EncryptionKey, Is.EqualTo(KeyA));

        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty);

        var encPath = Path.Combine(_tempDir, $"{slotName}.enc");
        var originalBytes = File.ReadAllBytes(encPath);
        Assert.DoesNotThrow(() => DSMEncryptor.Decrypt(originalBytes, KeyA));
    }
}
