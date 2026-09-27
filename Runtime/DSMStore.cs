#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>Owns a Config's Overrides: resolves Values, persists them to the Save File, and notifies Watchers.</summary>
    public sealed class DSMStore : IDisposable
    {
        private readonly DSMConfig _config;
        private readonly DSMSerializer _serializer;
        private readonly string? _saveFilePathOverride;
        private readonly CancellationTokenSource _lifetime;
        private readonly HashSet<string> _warnedKeys = new();
        private readonly Dictionary<string, List<ChannelWriter<JToken>>> _watchers = new();

        private Dictionary<string, JToken> _overrides = new();
        private int _autoSaveRequestVersion;
        private bool _autoSaveLoopRunning;

        /// <summary>Raised whenever a key's effective value changes, for deterministic test assertions without a PlayerLoop.</summary>
        internal event Action<string, JToken?>? ChangedForTests;

        public DSMStore(DSMConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serializer = new DSMSerializer();
            _saveFilePathOverride = null;
            _lifetime = new CancellationTokenSource();
        }

        internal DSMStore(DSMConfig config, string saveFilePath) : this(config)
        {
            _saveFilePathOverride = saveFilePath;
        }

        public DSMConfig Config => _config;

        public string SaveFilePath => _saveFilePathOverride ?? DSMPaths.ResolveSaveFilePath(_config);

        public bool IsDirty { get; private set; }

        public IReadOnlyCollection<string> OverrideKeys => _overrides.Keys;

        public T Get<T>(string key) => TryResolve<T>(key, out var value) ? value : default!;

        public T Get<T>(string key, T fallback) => TryResolve<T>(key, out var value) ? value : fallback;

        public void Set<T>(string key, T value)
        {
            if (!_config.TryGetEntry(key, out _)) WarnUndefinedOnce(key);

            var newToken = value is null ? JValue.CreateNull() : JToken.FromObject(value, _serializer.JsonSerializer);
            if (_overrides.TryGetValue(key, out var existingOverride) && JToken.DeepEquals(existingOverride, newToken))
                return;

            var before = GetEffectiveToken(key);
            _overrides[key] = newToken;
            RaiseIfChanged(key, before);
            MarkDirty();
        }

        public bool HasOverride(string key) => _overrides.ContainsKey(key);

        /// <summary>Sets an Override from a pre-built <see cref="JToken"/>, for editor code that already has one on hand. Behaves exactly like <see cref="Set{T}"/>.</summary>
        internal void SetToken(string key, JToken token)
        {
            if (!_config.TryGetEntry(key, out _)) WarnUndefinedOnce(key);

            if (_overrides.TryGetValue(key, out var existingOverride) && JToken.DeepEquals(existingOverride, token))
                return;

            var before = GetEffectiveToken(key);
            _overrides[key] = token;
            RaiseIfChanged(key, before);
            MarkDirty();
        }

        public void Reset(string key)
        {
            if (!_overrides.ContainsKey(key)) return;
            var before = GetEffectiveToken(key);
            _overrides.Remove(key);
            RaiseIfChanged(key, before);
            MarkDirty();
        }

        public void ResetAll()
        {
            if (_overrides.Count == 0) return;
            var watchedBefore = CaptureWatchedTokens();
            _overrides.Clear();
            NotifyWatchedChanges(watchedBefore);
            MarkDirty();
        }

        public JToken? GetEffectiveToken(string key)
        {
            if (_overrides.TryGetValue(key, out var overrideToken)) return overrideToken;
            if (_config.TryGetEntry(key, out var entry) && !string.IsNullOrEmpty(entry.DefaultJson))
            {
                try { return JToken.Parse(entry.DefaultJson); }
                catch { return null; }
            }
            return null;
        }

        public void Save()
        {
            var path = SaveFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmpPath = path + ".tmp";
            var root = new JObject();
            foreach (var kv in _overrides) root[kv.Key] = kv.Value;

            try
            {
                File.WriteAllText(tmpPath, root.ToString(Formatting.Indented));
                if (File.Exists(path)) File.Replace(tmpPath, path, null);
                else File.Move(tmpPath, path);
            }
            catch
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
                throw;
            }

            IsDirty = false;
        }

        public void Load()
        {
            var watchedBefore = CaptureWatchedTokens();
            var path = SaveFilePath;

            if (!File.Exists(path))
            {
                _overrides = new Dictionary<string, JToken>();
            }
            else
            {
                try
                {
                    var obj = JObject.Parse(File.ReadAllText(path));
                    _overrides = obj.Properties().ToDictionary(p => p.Name, p => p.Value);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"DSM: failed to parse save file at '{path}' ({e.GetType().Name}); using empty overrides.");
                    _overrides = new Dictionary<string, JToken>();
                }
            }

            NotifyWatchedChanges(watchedBefore);
        }

        public IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key)
        {
            return UniTaskAsyncEnumerable.Create<T>(async (writer, token) =>
            {
                var channel = Channel.CreateSingleConsumerUnbounded<JToken>();
                RegisterWatcher(key, channel.Writer);
                try
                {
                    if (GetEffectiveToken(key) != null)
                        await writer.YieldAsync(Get<T>(key));

                    await foreach (var changed in channel.Reader.ReadAllAsync(token))
                    {
                        if (changed == null) continue;
                        T converted;
                        try { converted = changed.ToObject<T>(_serializer.JsonSerializer)!; }
                        catch { continue; }
                        await writer.YieldAsync(converted);
                    }
                }
                finally
                {
                    UnregisterWatcher(key, channel.Writer);
                }
            });
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            foreach (var writers in _watchers.Values)
                foreach (var writer in writers)
                    writer.TryComplete();
            _watchers.Clear();
        }

        private bool TryResolve<T>(string key, out T value)
        {
            var hasDefinition = _config.TryGetEntry(key, out var entry);
            if (!hasDefinition) WarnUndefinedOnce(key);

            if (_overrides.TryGetValue(key, out var overrideToken))
            {
                try
                {
                    value = overrideToken.ToObject<T>(_serializer.JsonSerializer)!;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"DSM: override for key '{key}' could not be converted to {typeof(T).Name} ({e.GetType().Name}).");
                }
            }

            if (hasDefinition && !string.IsNullOrEmpty(entry.DefaultJson))
            {
                try
                {
                    value = JToken.Parse(entry.DefaultJson).ToObject<T>(_serializer.JsonSerializer)!;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"DSM: default for key '{key}' could not be parsed as {typeof(T).Name} ({e.GetType().Name}).");
                }
            }

            value = default!;
            return false;
        }

        private void WarnUndefinedOnce(string key)
        {
            if (!_warnedKeys.Add(key)) return;
            Debug.LogWarning($"DSM: key '{key}' has no entry definition in the DSM config.");
        }

        private void MarkDirty()
        {
            IsDirty = true;
            if (!_config.AutoSave) return;

            _autoSaveRequestVersion++;
            if (_autoSaveLoopRunning) return;

            _autoSaveLoopRunning = true;
            AutoSaveLoopAsync(_lifetime.Token).Forget();
        }

        private async UniTaskVoid AutoSaveLoopAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var observed = _autoSaveRequestVersion;
                    await UniTask.Delay(TimeSpan.FromSeconds(_config.AutoSaveDebounce), DelayType.Realtime, PlayerLoopTiming.Update, token);
                    if (_autoSaveRequestVersion == observed) break;
                }
                if (IsDirty) Save();
            }
            catch (OperationCanceledException)
            {
                // Store disposed or lifetime cancelled — nothing to persist.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _autoSaveLoopRunning = false;
            }
        }

        private Dictionary<string, JToken?> CaptureWatchedTokens()
        {
            var result = new Dictionary<string, JToken?>();
            foreach (var key in _watchers.Keys) result[key] = GetEffectiveToken(key);
            return result;
        }

        private void NotifyWatchedChanges(Dictionary<string, JToken?> before)
        {
            foreach (var kv in before)
                RaiseIfChanged(kv.Key, kv.Value);
        }

        private void RaiseIfChanged(string key, JToken? before)
        {
            var after = GetEffectiveToken(key);
            if (JToken.DeepEquals(before, after)) return;

            ChangedForTests?.Invoke(key, after);

            if (_watchers.TryGetValue(key, out var writers) && writers.Count > 0)
            {
                foreach (var writer in writers.ToArray())
                    writer.TryWrite(after!);
            }
        }

        private void RegisterWatcher(string key, ChannelWriter<JToken> writer)
        {
            if (!_watchers.TryGetValue(key, out var list))
            {
                list = new List<ChannelWriter<JToken>>();
                _watchers[key] = list;
            }
            list.Add(writer);
        }

        private void UnregisterWatcher(string key, ChannelWriter<JToken> writer)
        {
            if (!_watchers.TryGetValue(key, out var list)) return;
            list.Remove(writer);
            if (list.Count == 0) _watchers.Remove(key);
        }
    }
}
