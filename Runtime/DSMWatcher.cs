#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

public sealed class DSMWatcher
{
    private readonly Dictionary<string, List<Channel<object>>> _channels = new();
    private readonly Dictionary<string, object> _pending = new();
    private readonly object _lock = new();
    private bool _flushScheduled;

    // Returns an async enumerable that emits the current value immediately (if one exists),
    // then emits each subsequent value pushed via Notify().
    // Subscription is cleaned up automatically when the cancellation token is cancelled.
    public IUniTaskAsyncEnumerable<T> Watch<T>(string key, Func<(bool exists, T value)> currentValueProvider)
    {
        return UniTaskAsyncEnumerable.Create<T>(async (writer, token) =>
        {
            var (exists, current) = currentValueProvider();
            if (exists)
                await writer.YieldAsync(current);

            var channel = Channel.CreateSingleConsumerUnbounded<object>();
            Register(key, channel);

            try
            {
                await foreach (var value in channel.Reader.ReadAllAsync(token))
                {
                    if (value is T typedValue)
                        await writer.YieldAsync(typedValue);
                }
            }
            finally
            {
                Unregister(key, channel);
            }
        });
    }

    public void Notify(string key, object value)
    {
        lock (_lock)
        {
            _pending[key] = value;
            if (_flushScheduled) return;
            _flushScheduled = true;
        }
        ScheduleFlush();
    }

    // Delivery is deferred to the end of the frame so N Set()s of one key collapse into a
    // single notification carrying the latest value (WATCH-01). This only fires while a
    // PlayerLoop is running, which is why Flush() is also callable directly.
    private void ScheduleFlush()
    {
        UniTask.Void(async () =>
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            Flush();
        });
    }

    internal void Flush()
    {
        List<(Channel<object> Channel, object Value)>? deliveries = null;
        lock (_lock)
        {
            _flushScheduled = false;
            if (_pending.Count == 0) return;

            foreach (var entry in _pending)
            {
                if (!_channels.TryGetValue(entry.Key, out var channels)) continue;
                deliveries ??= new List<(Channel<object>, object)>();
                foreach (var channel in channels)
                    deliveries.Add((channel, entry.Value));
            }
            _pending.Clear();
        }

        if (deliveries == null) return;
        foreach (var delivery in deliveries)
            delivery.Channel.Writer.TryWrite(delivery.Value);
    }

    private void Register(string key, Channel<object> channel)
    {
        lock (_lock)
        {
            if (!_channels.ContainsKey(key))
                _channels[key] = new List<Channel<object>>();
            _channels[key].Add(channel);
        }
    }

    private void Unregister(string key, Channel<object> channel)
    {
        lock (_lock)
        {
            if (!_channels.TryGetValue(key, out var channels)) return;
            channels.Remove(channel);
            if (channels.Count == 0)
                _channels.Remove(key);
        }
    }
}
