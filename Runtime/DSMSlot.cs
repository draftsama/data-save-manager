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
    private readonly DSMSchema _schema;
    private Dictionary<string, JToken> _data = new();
    private readonly object _dataLock = new();
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _debounceLock = new();
    private long _debounceRequestVersion;
    private bool _debounceLoopRunning;
    private volatile bool _rotationInProgress;

    public DSMSlot(string slotName, DSMConfig config, DSMSerializer serializer, string saveDirectory, Type? constantType)
    {
        _slotName = slotName;
        _config = config;
        _serializer = serializer;
        _saveDirectory = saveDirectory;
        _constantType = constantType;
        _schema = DSMSchema.For(_constantType);
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        JToken token;
        if (_schema.TryGetExpectedType(key, out var expected) && !expected.IsAssignableFrom(typeof(T)))
        {
            if (_config.StrictSchema)
                throw new DSMSchemaViolationException(key, expected, typeof(T));

            Debug.LogWarning($"DSM: key '{key}' expected type '{expected.Name}' but got '{typeof(T).Name}' — coercing.");
            try
            {
                var coerced = JToken.FromObject(value, _serializer.JsonSerializer).ToObject(expected, _serializer.JsonSerializer);
                token = JToken.FromObject(coerced!, _serializer.JsonSerializer);
            }
            catch (Exception ex)
            {
                // Uncoercible value: fall back to storing it as-is rather than losing the
                // write entirely — lenient mode never throws, it only ever warns.
                // Never interpolate ex.Message: Newtonsoft conversion errors embed the
                // offending value, which would leak it into the log (T-03-01).
                Debug.LogWarning(
                    $"DSM: could not coerce key '{key}' from '{typeof(T).Name}' to '{expected.Name}' " +
                    $"({ex.GetType().Name}) — storing as-is.");
                try
                {
                    token = JToken.FromObject(value, _serializer.JsonSerializer);
                }
                catch (Exception fallbackEx)
                {
                    // Unserializable value: drop the write rather than throw — the lenient
                    // mode invariant is "warn, never throw" (no value in the message).
                    Debug.LogWarning(
                        $"DSM: key '{key}' value of type '{typeof(T).Name}' is not serializable " +
                        $"({fallbackEx.GetType().Name}) — write dropped.");
                    return;
                }
            }
        }
        else
        {
            token = JToken.FromObject(value, _serializer.JsonSerializer);
        }

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
        if (_schema.TryGetExpectedType(key, out var expectedGet) && !expectedGet.IsAssignableFrom(typeof(T)))
        {
            if (_config.StrictSchema)
                throw new DSMSchemaViolationException(key, expectedGet, typeof(T));

            Debug.LogWarning($"DSM: key '{key}' expected type '{expectedGet.Name}' but requested as '{typeof(T).Name}'.");
        }

        JToken? token;
        lock (_dataLock)
        {
            if (!_data.TryGetValue(key, out token)) return defaultValue;
        }
        try
        {
            return token.ToObject<T>(_serializer.JsonSerializer) ?? defaultValue;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"DSM: key '{key}' could not be read as '{typeof(T).Name}' ({ex.GetType().Name}).");
            return defaultValue;
        }
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
            var json = SerializeSnapshot();
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

            try
            {
                var deserialized = _serializer.Deserialize(json);
                lock (_dataLock)
                {
                    _data = deserialized;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DSM: slot '{_slotName}' has malformed save data, seeding defaults: {ex.Message}");
                SeedDefaults();
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
            var json = SerializeSnapshot();
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

            try
            {
                var deserialized = _serializer.Deserialize(json);
                lock (_dataLock)
                {
                    _data = deserialized;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DSM: slot '{_slotName}' has malformed save data, seeding defaults: {ex.Message}");
                SeedDefaults();
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    // --- Key rotation (ENC-02): stage/commit re-encrypt, reusing _ioGate + ReplaceFile ---

    /// <summary>
    /// Decrypts the slot's on-disk .enc file with <paramref name="oldKey"/>, re-encrypts it
    /// with <paramref name="newKey"/>, and writes the result to a sibling .tmp file — verifying
    /// the .tmp decrypts with <paramref name="newKey"/> before returning. Does not touch the
    /// destination .enc file. Runs under the same _ioGate that serializes Save/Load so a
    /// concurrent autosave cannot race the stage. Throws (propagating to the caller for
    /// cleanup) on any decrypt/encrypt/IO failure — never logs oldKey/newKey.
    /// </summary>
    internal async UniTask StageReencryptAsync(string oldKey, string newKey)
    {
        await _ioGate.WaitAsync();
        try
        {
            var encPath = GetSavePath();
            var tmpPath = GetRotationTempPath();

            var bytes = await File.ReadAllBytesAsync(encPath);
            var json = DSMEncryptor.Decrypt(bytes, oldKey);
            var reencrypted = DSMEncryptor.Encrypt(json, newKey);
            await File.WriteAllBytesAsync(tmpPath, reencrypted);

            try
            {
                // Verify the staged file actually decrypts with the new key before the
                // stage counts as successful — never leave a bad .tmp for the commit phase.
                var verifyBytes = await File.ReadAllBytesAsync(tmpPath);
                DSMEncryptor.Decrypt(verifyBytes, newKey);
            }
            catch
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
                throw;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Atomically renames the staged .tmp (written by <see cref="StageReencryptAsync"/>) into
    /// the slot's .enc file using the existing <see cref="ReplaceFile"/> primitive. Runs under
    /// _ioGate.
    /// </summary>
    internal void CommitReencrypt()
    {
        _ioGate.Wait();
        try
        {
            var encPath = GetSavePath();
            var tmpPath = GetRotationTempPath();
            // Idempotent: a missing .tmp means this slot was already committed (e.g. a
            // best-effort retry after a mid-burst failure, or journal recovery re-running).
            if (!File.Exists(tmpPath)) return;
            ReplaceFile(tmpPath, encPath);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Deletes a leftover staged .tmp file (used by the manager to abort a rotation after a
    /// staging failure). Never throws — cleanup must not mask the original failure.
    /// </summary>
    internal void CleanupStagedTemp()
    {
        try
        {
            var tmpPath = GetRotationTempPath();
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
        catch
        {
            // Best-effort cleanup only — never mask the original staging failure.
        }
    }

    internal void BeginRotation() => _rotationInProgress = true;

    internal void EndRotation() => _rotationInProgress = false;

    // Rotation staging uses a temp name distinct from Save/SaveAsync's "{slot}.enc.tmp"
    // so an ordinary autosave can never clobber or consume a staged re-encrypted file.
    internal string GetRotationTempPath() => GetSavePath() + ".rotate.tmp";

    public IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) =>
        _watcher.Watch<T>(key, () =>
        {
            JToken? token;
            lock (_dataLock)
            {
                if (!_data.TryGetValue(key, out token)) return (false, default!);
            }
            try
            {
                var value = token.ToObject<T>(_serializer.JsonSerializer);
                return value is not null ? (true, value) : (false, default!);
            }
            catch
            {
                return (false, default!);
            }
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
        // Never persist while a key rotation is in flight — an autosave here would
        // write this slot's .enc with the still-current (old) key, leaving it on a
        // different key than the slots the rotation is committing. The in-memory
        // change is retained and will be saved on the next Set() after rotation ends.
        if (_rotationInProgress) return;
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

            // A rotation may have started while this loop was waiting; skip the write
            // rather than persist stale-key data mid-rotation (see ScheduleSave).
            if (_rotationInProgress) return;
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

    private string SerializeSnapshot()
    {
        Dictionary<string, JToken> snapshot;
        lock (_dataLock)
        {
            snapshot = new Dictionary<string, JToken>(_data);
        }
        return _serializer.Serialize(snapshot, _config.PrettyPrint);
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
