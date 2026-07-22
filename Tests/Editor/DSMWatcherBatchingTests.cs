#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class DSMWatcherBatchingTests
{
    private DSMConfig _config = null!;
    private DSMSerializer _serializer = null!;
    private string _tempDir = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _config = DSMTestConfig.Create();
        _serializer = new DSMSerializer();
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_WatchBatch_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    }

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Set_ManyTimesSameKeyInOneFrame_DeliversLatestValueOnce()
    {
        var slot = NewSlot("coalesce");
        var received = Collect(slot, "hp");
        await DrainAsync();

        slot.Set("hp", 1);
        slot.Set("hp", 2);
        slot.Set("hp", 3);
        slot.FlushWatchers();
        await DrainAsync();

        Assert.That(received, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public async Task Set_DistinctKeysInOneFrame_EachSubscriberGetsItsOwnValueOnce()
    {
        var slot = NewSlot("per-key");
        var hp = Collect(slot, "hp");
        var mp = Collect(slot, "mp");
        await DrainAsync();

        slot.Set("hp", 10);
        slot.Set("mp", 20);
        slot.FlushWatchers();
        await DrainAsync();

        Assert.That(hp, Is.EqualTo(new[] { 10 }));
        Assert.That(mp, Is.EqualTo(new[] { 20 }));
    }

    [Test]
    public async Task Set_AcrossTwoFrames_DeliversOneValuePerFrame()
    {
        var slot = NewSlot("multi-frame");
        var received = Collect(slot, "hp");
        await DrainAsync();

        slot.Set("hp", 1);
        slot.FlushWatchers();
        await DrainAsync();

        slot.Set("hp", 2);
        slot.FlushWatchers();
        await DrainAsync();

        Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task Subscribe_WhenKeyAlreadyHasValue_ReplaysCurrentValueImmediately()
    {
        var slot = NewSlot("replay");
        slot.Set("hp", 5);
        slot.FlushWatchers();
        await DrainAsync();

        var received = Collect(slot, "hp");
        await DrainAsync();

        Assert.That(received, Is.EqualTo(new[] { 5 }));
    }

    [Test]
    public async Task Set_BeforeFlush_DeliversNothingSynchronously()
    {
        var slot = NewSlot("deferred");
        var received = Collect(slot, "hp");
        await DrainAsync();

        slot.Set("hp", 99);

        // Assert without yielding: the scheduled PlayerLoop flush is driven by the editor
        // update loop, so any await here could legitimately deliver the value and make the
        // assertion flaky. Checking on the Set() thread is what pins WATCH-01 — Notify must
        // buffer, never write to subscriber channels inline.
        Assert.That(received, Is.Empty);

        slot.FlushWatchers();
        await DrainAsync();

        Assert.That(received, Is.EqualTo(new[] { 99 }));
    }

    private DSMSlot NewSlot(string name) => new(name, _config, _serializer, _tempDir, null);

    private List<int> Collect(DSMSlot slot, string key)
    {
        var sink = new List<int>();
        var token = _cts.Token;
        UniTask.Void(async () =>
        {
            try
            {
                await foreach (var value in slot.WatchAsync<int>(key).WithCancellation(token))
                    sink.Add(value);
            }
            catch (OperationCanceledException)
            {
            }
        });
        return sink;
    }

    private static async UniTask DrainAsync()
    {
        // Two yields: one hands the channel write to the watcher's reader loop, one lets the
        // collector's await-foreach body append the value before the assertion runs.
        await UniTask.Yield();
        await UniTask.Yield();
    }
}
