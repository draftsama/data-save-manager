#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class DSMSlot
{
    private readonly string _slotName;
    private readonly DSMConfig _config;
    private readonly DSMSerializer _serializer;
    private readonly DSMWatcher _watcher = new();
    private readonly string _saveDirectory;
    private readonly Type? _constantType;
    private Dictionary<string, JToken> _data = new();
    private readonly object _dataLock = new();
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _debounceLock = new();
    private long _debounceRequestVersion;
    private bool _debounceLoopRunning;

    public DSMSlot(string slotName, DSMConfig config, DSMSerializer serializer, string saveDirectory, Type? constantType)
    {
        _slotName = slotName;
        _config = config;
        _serializer = serializer;
        _saveDirectory = saveDirectory;
        _constantType = constantType;
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        var token = JToken.FromObject(value, _serializer.JsonSerializer);
        lock (_dataLock)
        {
            _data[key] = token;
        }
        _watcher.Notify(key, value);
        if (_config.AutoSave)
            ScheduleSave();
    }

    public T Get<T>(string key, T defaultValue)
    {
        JToken? token;
        lock (_dataLock)
        {
            if (!_data.TryGetValue(key, out token)) return defaultValue;
        }
        return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
    }

    public bool Has(string key)
    {
        lock (_dataLock)
        {
            return _data.ContainsKey(key);
        }
    }

    public void Delete(string key)
    {
        lock (_dataLock)
        {
            _data.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_dataLock)
        {
            _data.Clear();
        }
    }

    public void Save()
    {
        _ioGate.Wait();
        try
        {
            CancelDebounce();
            var json = _serializer.Serialize(_data, _config.PrettyPrint);
            var path = GetSavePath();
            var tmpPath = path + ".tmp";

            try
            {
                if (_config.Encrypt)
                    File.WriteAllBytes(tmpPath, DSMEncryptor.Encrypt(json, _config.EncryptionKey));
                else
                    File.WriteAllText(tmpPath, json);

                ReplaceFile(tmpPath, path);
            }
            catch
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
                throw;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public void Load()
    {
        _ioGate.Wait();
        try
        {
            var path = GetLoadPath();
            if (path is null)
            {
                SeedDefaults();
                return;
            }

            var json = IsEncryptedFile(path)
                ? DSMEncryptor.Decrypt(File.ReadAllBytes(path), _config.EncryptionKey)
                : File.ReadAllText(path);

            var deserialized = _serializer.Deserialize(json);
            lock (_dataLock)
            {
                _data = deserialized;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async UniTask SaveAsync()
    {
        await _ioGate.WaitAsync();
        try
        {
            CancelDebounce();
            var json = _serializer.Serialize(_data, _config.PrettyPrint);
            var path = GetSavePath();
            var tmpPath = path + ".tmp";

            try
            {
                if (_config.Encrypt)
                    await File.WriteAllBytesAsync(tmpPath, DSMEncryptor.Encrypt(json, _config.EncryptionKey));
                else
                    await File.WriteAllTextAsync(tmpPath, json);

                ReplaceFile(tmpPath, path);
            }
            catch
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
                throw;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async UniTask LoadAsync()
    {
        await _ioGate.WaitAsync();
        try
        {
            var path = GetLoadPath();
            if (path is null)
            {
                SeedDefaults();
                return;
            }

            string json;
            if (IsEncryptedFile(path))
            {
                var bytes = await File.ReadAllBytesAsync(path);
                json = DSMEncryptor.Decrypt(bytes, _config.EncryptionKey);
            }
            else
            {
                json = await File.ReadAllTextAsync(path);
            }

            var deserialized = _serializer.Deserialize(json);
            lock (_dataLock)
            {
                _data = deserialized;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) =>
        _watcher.Watch<T>(key, () =>
        {
            if (!_data.TryGetValue(key, out var token)) return (false, default!);
            var value = token.ToObject<T>(_serializer.JsonSerializer);
            return value is not null ? (true, value) : (false, default!);
        });

    private void SeedDefaults()
    {
        lock (_dataLock)
        {
            _data = new Dictionary<string, JToken>();
            if (_constantType == null) return;
            foreach (var field in _constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var value = field.GetValue(null);
                if (value == null) continue;
                try { _data[field.Name] = JToken.FromObject(value, _serializer.JsonSerializer); }
                catch (Exception ex) { Debug.LogWarning($"DSM: Could not seed default for '{field.Name}': {ex.Message}"); }
            }
        }
    }

    // Single long-lived debounce loop (D-01/CONC-02): each Set() bumps a request
    // version and, only if no loop is already running, starts ONE loop that waits
    // the debounce window and re-checks the version — if a newer request arrived
    // during the wait it loops again, otherwise it saves and exits. There is no
    // per-Set() CancellationTokenSource, so the disposal race cannot occur.
    private void ScheduleSave()
    {
        lock (_debounceLock)
        {
            _debounceRequestVersion++;
            if (_debounceLoopRunning) return;
            _debounceLoopRunning = true;
        }
        DebouncedSaveLoopAsync().Forget();
    }

    private async UniTaskVoid DebouncedSaveLoopAsync()
    {
        try
        {
            while (true)
            {
                long observedVersion;
                lock (_debounceLock)
                {
                    observedVersion = _debounceRequestVersion;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_config.AutoSaveDebounce));

                lock (_debounceLock)
                {
                    if (_debounceRequestVersion != observedVersion)
                        continue; // a newer Set() arrived during the wait — loop again
                    break; // no newer request since the wait started — settle and save
                }
            }

            await SaveAsync();
        }
        finally
        {
            lock (_debounceLock)
            {
                _debounceLoopRunning = false;
            }
        }
    }

    private void CancelDebounce()
    {
        // The debounce loop is self-terminating — there is no per-call
        // CancellationTokenSource to cancel/dispose here anymore. An explicit
        // Save()/SaveAsync() simply proceeds; any still-pending loop iteration
        // will observe up-to-date state the next time it wakes. Safe no-op,
        // never throws when nothing is pending.
    }

    private string GetSavePath()
    {
        Directory.CreateDirectory(_saveDirectory);
        var ext = _config.Encrypt ? "enc" : "json";
        return Path.Combine(_saveDirectory, $"{_slotName}.{ext}");
    }

    private string? GetLoadPath()
    {
        var encPath = Path.Combine(_saveDirectory, $"{_slotName}.enc");
        var jsonPath = Path.Combine(_saveDirectory, $"{_slotName}.json");

        // Prefer encrypted file when both exist
        if (File.Exists(encPath)) return encPath;
        if (File.Exists(jsonPath)) return jsonPath;
        return null;
    }

    private static bool IsEncryptedFile(string path) =>
        Path.GetExtension(path).Equals(".enc", StringComparison.OrdinalIgnoreCase);

    // Portable atomic-rename: File.Move(src, dest, overwrite) is not available at Unity's
    // API compatibility level, so use File.Replace when the destination already exists
    // (atomic on the file system) and fall back to a plain Move for the first-ever save.
    private static void ReplaceFile(string tmpPath, string destPath)
    {
        if (File.Exists(destPath))
            File.Replace(tmpPath, destPath, null);
        else
            File.Move(tmpPath, destPath);
    }
}
