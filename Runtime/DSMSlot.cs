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
    private Dictionary<string, JToken> _data = new();
    private CancellationTokenSource? _debounceCts;

    public DSMSlot(string slotName, DSMConfig config, DSMSerializer serializer)
    {
        _slotName = slotName;
        _config = config;
        _serializer = serializer;
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        _data[key] = JToken.FromObject(value, _serializer.JsonSerializer);
        _watcher.Notify(key, value);
        if (_config.AutoSave)
            ScheduleSave();
    }

    public T Get<T>(string key, T defaultValue)
    {
        if (!_data.TryGetValue(key, out var token)) return defaultValue;
        return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Delete(string key) => _data.Remove(key);

    public void Clear() => _data.Clear();

    public void Save()
    {
        CancelDebounce();
        var json = _serializer.Serialize(_data, _config.PrettyPrint);
        var path = GetSavePath();

        if (_config.Encrypt)
            File.WriteAllBytes(path, DSMEncryptor.Encrypt(json, _config.EncryptionKey));
        else
            File.WriteAllText(path, json);
    }

    public void Load()
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

        _data = _serializer.Deserialize(json);
    }

    public async UniTask SaveAsync()
    {
        CancelDebounce();
        var json = _serializer.Serialize(_data, _config.PrettyPrint);
        var path = GetSavePath();

        if (_config.Encrypt)
            await File.WriteAllBytesAsync(path, DSMEncryptor.Encrypt(json, _config.EncryptionKey));
        else
            await File.WriteAllTextAsync(path, json);
    }

    public async UniTask LoadAsync()
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

        _data = _serializer.Deserialize(json);
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
        _data = new Dictionary<string, JToken>();
        Type? constantType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
                    { constantType = t; break; }
                }
            }
            catch { }
            if (constantType != null) break;
        }
        if (constantType == null) return;
        foreach (var field in constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null);
            if (value == null) continue;
            try { _data[field.Name] = JToken.FromObject(value, _serializer.JsonSerializer); }
            catch { }
        }
    }

    private void ScheduleSave()
    {
        // Reset the debounce timer on every Set() call
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        DebouncedSaveAsync(_debounceCts.Token).Forget();
    }

    private async UniTaskVoid DebouncedSaveAsync(CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(_config.AutoSaveDebounce), cancellationToken: token);
        await SaveAsync();
    }

    private void CancelDebounce()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    private string GetSavePath()
    {
        var dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);
        var ext = _config.Encrypt ? "enc" : "json";
        return Path.Combine(dir, $"{_slotName}.{ext}");
    }

    private string? GetLoadPath()
    {
        var dir = GetSaveDirectory();
        var encPath = Path.Combine(dir, $"{_slotName}.enc");
        var jsonPath = Path.Combine(dir, $"{_slotName}.json");

        // Prefer encrypted file when both exist
        if (File.Exists(encPath)) return encPath;
        if (File.Exists(jsonPath)) return jsonPath;
        return null;
    }

    private string GetSaveDirectory() =>
        string.IsNullOrEmpty(_config.SavePath)
            ? Path.Combine(Application.persistentDataPath, "DSM")
            : _config.SavePath;

    private static bool IsEncryptedFile(string path) =>
        Path.GetExtension(path).Equals(".enc", StringComparison.OrdinalIgnoreCase);
}
