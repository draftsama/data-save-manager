#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class DSMSlotManager
{
    // Marker file in SaveDirectory listing the slot names being rotated (one per
    // line). Its presence means a rotation reached the commit phase; a fresh
    // DSMSlotManager replays the pending renames before loading anything (ENC-02).
    public const string RotationJournalName = ".dsm-rotation";

    private readonly Dictionary<string, DSMSlot> _slots = new();
    private readonly DSMConfig _config;
    private readonly DSMSerializer _serializer = new();
    private DSMSlot _activeSlot;
    private readonly object _slotsLock = new();

    private static Type? s_constantType;

    public DSMSlot ActiveSlot => _activeSlot;
    public string SaveDirectory => DSMPaths.GetSaveDirectory(_config.SavePath);

    public DSMSlotManager(DSMConfig config)
    {
        _config = config;
        RecoverInterruptedRotation();
        _activeSlot = GetOrCreateSlot(config.DefaultSlot);
        _activeSlot.Load();
    }

    public void UseSlot(string name)
    {
        _activeSlot = GetOrCreateSlot(name);
        _activeSlot.Load();
    }

    public DSMSlot GetSlot(string name) => GetOrCreateSlot(name);

    public void DeleteSlot(string name)
    {
        DSMSlotNameValidator.Validate(name);
        lock (_slotsLock)
        {
            _slots.Remove(name);
        }
        var dir = SaveDirectory;
        var jsonPath = Path.Combine(dir, $"{name}.json");
        var encPath = Path.Combine(dir, $"{name}.enc");
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        if (File.Exists(encPath)) File.Delete(encPath);
    }

    public string[] GetAllSlots()
    {
        var dir = SaveDirectory;
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".enc",  StringComparison.OrdinalIgnoreCase))
                names.Add(Path.GetFileNameWithoutExtension(file)!);
        }
        var result = new string[names.Count];
        names.CopyTo(result);
        return result;
    }

    public void SaveActiveSlot() => _activeSlot.Save();

    /// <summary>
    /// Re-encrypts every encrypted slot from the current key to <paramref name="newKey"/> and
    /// commits the new key to <see cref="DSMConfig"/> only after every slot has been staged
    /// and committed. Staging is all-or-nothing: any failure cleans up the slots staged so far
    /// and leaves the config key and every original .enc file untouched (no mixed state). A
    /// rotation journal is written just before the commit burst so an interruption there is
    /// recoverable by <see cref="RecoverInterruptedRotation"/> on the next construction.
    /// </summary>
    public async UniTask RotateEncryptionKeyAsync(string newKey)
    {
        DSMEncryptionKey.Validate(newKey);

        if (!_config.Encrypt)
            throw new InvalidOperationException("DSM: key rotation requires encryption to be enabled.");

        var oldKey = _config.EncryptionKey;
        try
        {
            DSMEncryptionKey.Validate(oldKey);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("DSM: key rotation requires a valid current encryption key.");
        }

        var dir = SaveDirectory;
        var encryptedSlotNames = GetAllSlots()
            .Where(name => File.Exists(Path.Combine(dir, $"{name}.enc")))
            .ToArray();

        // STAGE — decrypt with oldKey, re-encrypt with newKey, write {slot}.enc.tmp.
        // On ANY failure, clean up every slot staged so far and rethrow: the config key
        // is never touched here, so every original .enc file remains readable with oldKey.
        var stagedSlots = new List<DSMSlot>();
        try
        {
            foreach (var name in encryptedSlotNames)
            {
                var slot = GetOrCreateSlot(name);
                await slot.StageReencryptAsync(oldKey, newKey);
                stagedSlots.Add(slot);
            }
        }
        catch
        {
            foreach (var slot in stagedSlots)
                slot.CleanupStagedTemp();
            throw;
        }

        // Mark commit-in-progress before mutating any file, so an interruption during the
        // commit burst below is recoverable on the next DSMSlotManager construction.
        WriteRotationJournal(dir, encryptedSlotNames);

        // COMMIT — rename every staged .tmp into place.
        foreach (var slot in stagedSlots)
            slot.CommitReencrypt();

        // Commit the new key LAST, only after every slot commit succeeded.
        _config.SetEncryptionKey(newKey);

        DeleteRotationJournal(dir);
    }

    /// <summary>
    /// If a rotation journal is present in <see cref="SaveDirectory"/> (meaning a previous
    /// rotation reached the commit phase and was interrupted), finishes the pending renames for
    /// every listed slot that still has a staged .tmp file, then deletes the journal. Called
    /// from the constructor before the active slot loads, so no slot is left unreadable.
    /// </summary>
    private void RecoverInterruptedRotation()
    {
        var journalPath = Path.Combine(SaveDirectory, RotationJournalName);
        if (!File.Exists(journalPath)) return;

        var slotNames = File.ReadAllLines(journalPath).Where(l => !string.IsNullOrWhiteSpace(l));
        foreach (var name in slotNames)
        {
            var tmpPath = Path.Combine(SaveDirectory, $"{name}.enc.tmp");
            if (!File.Exists(tmpPath)) continue; // already committed before the interruption

            var slot = GetOrCreateSlot(name);
            slot.CommitReencrypt();
        }

        File.Delete(journalPath);
    }

    private static void WriteRotationJournal(string dir, IEnumerable<string> slotNames)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, RotationJournalName), slotNames);
    }

    private static void DeleteRotationJournal(string dir)
    {
        var path = Path.Combine(dir, RotationJournalName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private DSMSlot GetOrCreateSlot(string name)
    {
        DSMSlotNameValidator.Validate(name);
        lock (_slotsLock)
        {
            if (_slots.TryGetValue(name, out var slot)) return slot;
            slot = new DSMSlot(name, _config, _serializer, SaveDirectory, ResolveConstantType());
            _slots[name] = slot;
            return slot;
        }
    }

    private static Type? ResolveConstantType()
    {
        if (s_constantType != null) return s_constantType;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
                        return s_constantType = t;
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException) { }
            catch (Exception ex) { Debug.LogWarning($"DSM: {ex.Message}"); }
        }
        return null;
    }
}
