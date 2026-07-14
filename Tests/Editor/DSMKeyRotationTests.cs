#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
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
        await AssertRotateThrows<DSMEncryptionException>(manager, KeyB);

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

        var tmpPath = encPath + ".rotate.tmp";
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
    public async Task Rotate_ToWeakKey_Rejected()
    {
        // Arrange
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        const string slotName = "weak-key-slot";
        var slot = manager.GetSlot(slotName);
        slot.Set("value", "unchanged");
        slot.Save();

        // Act / Assert — empty and too-short keys are rejected
        await AssertRotateThrows<ArgumentException>(manager, "");
        await AssertRotateThrows<ArgumentException>(manager, "short");

        // Assert — config key unchanged and no slot files modified
        Assert.That(config.EncryptionKey, Is.EqualTo(KeyA));

        var leftoverTmp = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.That(leftoverTmp, Is.Empty);

        var encPath = Path.Combine(_tempDir, $"{slotName}.enc");
        var originalBytes = File.ReadAllBytes(encPath);
        Assert.DoesNotThrow(() => DSMEncryptor.Decrypt(originalBytes, KeyA));
    }

    [Test]
    public void Decrypt_LegacyFormat_ThrowsDistinguishableError()
    {
        // Regression test for CR-03: a pre-DSM2 file (no magic/version prefix) must fail with
        // a distinguishable "unsupported legacy format" error, not the generic
        // corrupt-or-wrong-key integrity message.
        var legacyIv = new byte[16];
        var legacySalt = new byte[32];
        RandomNumberGenerator.Fill(legacyIv);
        RandomNumberGenerator.Fill(legacySalt);
        var legacyBuffer = legacyIv.Concat(legacySalt).Concat(new byte[32]).ToArray();

        var ex = Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(legacyBuffer, KeyA));
        Assert.That(ex!.Message, Does.Contain("legacy format"));
    }

    [Test]
    public async Task Rotate_ConcurrentAutosave_DoesNotCorruptOrThrow()
    {
        // Regression test for CR-01: a debounced autosave firing on a rotating slot between
        // StageReencryptAsync and CommitReencrypt must not clobber the staged file or throw.
        var config = DSMTestConfig.Create(autoSave: true, autoSaveDebounce: 0.02f, encrypt: true,
            encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        // Enough slots to widen the stage/commit window so the concurrent autosave loop
        // below has a real chance of firing mid-rotation.
        var names = Enumerable.Range(0, 25).Select(i => $"slot-{i}").ToArray();
        foreach (var name in names)
        {
            var slot = manager.GetSlot(name);
            slot.Set("value", name);
            await slot.SaveAsync();
        }

        var target = manager.GetSlot(names[0]);

        using var cts = new CancellationTokenSource();
        var autosaveLoop = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.IsCancellationRequested)
            {
                target.Set("value", $"concurrent-{i++}"); // AutoSave schedules a debounced save
                await Task.Delay(5);
            }
        });

        Exception? rotateException = null;
        try
        {
            await manager.RotateEncryptionKeyAsync(KeyB);
        }
        catch (Exception ex)
        {
            rotateException = ex;
        }

        cts.Cancel();
        try { await autosaveLoop; } catch (OperationCanceledException) { }

        Assert.That(rotateException, Is.Null,
            $"rotation must not throw when autosave fires concurrently, got: {rotateException}");

        // The rotated file must be readable with the new key — not corrupted, not stuck on old key.
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, $"{names[0]}.enc"));
        Assert.DoesNotThrow(() => DSMEncryptor.Decrypt(bytes, KeyB));
        Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(bytes, KeyA));
    }

    [Test]
    public async Task Rotate_ConcurrentCall_SecondRejected()
    {
        // Regression test for WR-02: a second rotation must not be allowed to start while
        // one is already in flight.
        var config = DSMTestConfig.Create(encrypt: true, encryptionKey: KeyA, savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        var names = Enumerable.Range(0, 15).Select(i => $"slot-{i}").ToArray();
        foreach (var name in names)
        {
            var slot = manager.GetSlot(name);
            slot.Set("value", name);
            await slot.SaveAsync();
        }

        // RotateEncryptionKeyAsync acquires the rotation gate synchronously before its first
        // await, so this call has already claimed the gate by the time the line returns.
        var rotateTask1 = manager.RotateEncryptionKeyAsync(KeyB);

        var thrown = false;
        try
        {
            await manager.RotateEncryptionKeyAsync(KeyC);
        }
        catch (InvalidOperationException)
        {
            thrown = true;
        }

        await rotateTask1;
        Assert.That(thrown, Is.True, "a second concurrent rotation call must be rejected");
    }

    // NUnit's Assert.ThrowsAsync blocks the calling thread on the returned Task via its
    // internal AsyncToSyncAdapter; awaiting a UniTask inside that delegate needs the same
    // (now-blocked) main thread to resume its continuation, which deadlocks the Editor.
    // Awaiting the rotation directly from this already-async [Test] method avoids the adapter.
    private static async Task AssertRotateThrows<TException>(DSMSlotManager manager, string newKey)
        where TException : Exception
    {
        TException? thrown = null;
        try
        {
            await manager.RotateEncryptionKeyAsync(newKey);
        }
        catch (TException ex)
        {
            thrown = ex;
        }
        Assert.That(thrown, Is.Not.Null, $"expected {typeof(TException).Name} to be thrown");
    }
}
