#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

    [Test]
    public async Task ConcurrentSetAndLoad_NeverCorruptsData()
    {
        // Arrange — seed a real save file so Load() always has something to reload
        var seedSlot = new DSMSlot("set-load-matrix", _config, _serializer, _tempDir, null);
        seedSlot.Set("seed", 1);
        seedSlot.Save();

        var slot = new DSMSlot("set-load-matrix", _config, _serializer, _tempDir, null);
        const int count = 50;

        // Act — interleave Set (new keys, guarded by _dataLock) and Load (wholesale _data
        // reassignment, guarded by _dataLock) concurrently. Task.WhenAll surfaces any
        // exception from a torn/corrupted _data reference.
        var setTasks = Enumerable.Range(0, count).Select(i => Task.Run(() => slot.Set($"key{i}", i)));
        var loadTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() => slot.Load()));
        await Task.WhenAll(setTasks.Concat(loadTasks));

        // Assert — final state is internally consistent: every present key resolves to
        // its correct, non-default value (no exception surfaced, no torn dictionary
        // reference), and the dictionary remains queryable after the race.
        for (var i = 0; i < count; i++)
        {
            if (slot.Has($"key{i}"))
                Assert.That(slot.Get($"key{i}", -1), Is.EqualTo(i));
        }
        Assert.That(slot.Get("does-not-exist", -999), Is.EqualTo(-999));
    }

    [Test]
    public async Task ConcurrentSetAndSaveAsync_FinalFileParses()
    {
        // Arrange
        var slot = new DSMSlot("set-save-matrix", _config, _serializer, _tempDir, null);
        const int count = 50;

        // Act — interleave Set and SaveAsync concurrently
        var setTasks = Enumerable.Range(0, count).Select(i => Task.Run(() => slot.Set($"key{i}", i)));
        var saveTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () => await slot.SaveAsync()));
        await Task.WhenAll(setTasks.Concat(saveTasks));

        // Final settle save so the persisted file reflects a well-defined end state
        await slot.SaveAsync();

        // Assert — persisted file always parses via DSMSerializer.Deserialize, and every
        // key present in the file is a valid subset of the expected in-memory snapshot
        var destPath = Path.Combine(_tempDir, "set-save-matrix.json");
        Assert.That(File.Exists(destPath), Is.True);
        var json = File.ReadAllText(destPath);
        Dictionary<string, JToken> persisted = null!;
        Assert.DoesNotThrow(() => persisted = _serializer.Deserialize(json));
        foreach (var (key, token) in persisted)
        {
            Assert.That(slot.Has(key), Is.True, $"persisted key '{key}' must be present in in-memory state");
            var expected = int.Parse(key.Replace("key", ""));
            Assert.That((int)token, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task MultiWatcher_AllSubscribersReceiveValue()
    {
        // Arrange — TEST-03: multiple WatchAsync<T> subscribers on one key, all must
        // observe a Set()'s value under concurrent notify. Timeout token bounds the test
        // so a regression in DSMWatcher hangs the test instead of the whole test run.
        var slot = new DSMSlot("multi-watch", _config, _serializer, _tempDir, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            var subscriber1 = WaitForFirstValueAsync(slot, cts.Token);
            var subscriber2 = WaitForFirstValueAsync(slot, cts.Token);
            var subscriber3 = WaitForFirstValueAsync(slot, cts.Token);

            // Subscriber registration (channel creation) completes synchronously up to the
            // point each subscriber suspends awaiting a value, before this call returns
            // control here — but yield once as a defensive margin before firing Set().
            await UniTask.Yield();

            slot.Set("score", 77);

            var results = await UniTask.WhenAll(new[] { subscriber1, subscriber2, subscriber3 });

            Assert.That(results, Is.All.EqualTo(77));
        }
        finally
        {
            cts.Cancel();
        }
    }

    private static async UniTask<int> WaitForFirstValueAsync(DSMSlot slot, CancellationToken token)
    {
        await foreach (var value in slot.WatchAsync<int>("score").WithCancellation(token))
            return value;
        throw new InvalidOperationException("watch stream ended before producing a value");
    }
}
