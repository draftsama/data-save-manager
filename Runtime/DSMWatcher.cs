#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

public sealed class DSMWatcher
{
    private readonly Dictionary<string, List<Channel<object>>> _channels = new();

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
        if (!_channels.TryGetValue(key, out var channels)) return;

        foreach (var channel in channels)
            channel.Writer.TryWrite(value);
    }

    private void Register(string key, Channel<object> channel)
    {
        if (!_channels.ContainsKey(key))
            _channels[key] = new List<Channel<object>>();
        _channels[key].Add(channel);
    }

    private void Unregister(string key, Channel<object> channel)
    {
        if (!_channels.TryGetValue(key, out var channels)) return;
        channels.Remove(channel);
        if (channels.Count == 0)
            _channels.Remove(key);
    }
}
