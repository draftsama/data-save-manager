#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class DSM
{
    private static DSMSlotManager? s_manager;
    private static readonly List<IDSMMigration> s_migrations = new();

    private static DSMSlotManager Manager => s_manager ??= Initialize();

    /// <summary>Overrides the auto-loaded DSMConfig. Call before any other DSM method to use a custom config.</summary>
    public static void Configure(DSMConfig config)
    {
        if (s_manager != null)
            Application.quitting -= s_manager.SaveActiveSlot;
        s_manager = new DSMSlotManager(config, new DSMMigrationRunner(s_migrations));
        Application.quitting += s_manager.SaveActiveSlot;
    }

    // --- Migration registry ---

    /// <summary>Registers a save migration applied lazily to each slot on load. Must be called before the first DSM access (before the manager is built), else throws <see cref="InvalidOperationException"/>.</summary>
    public static void RegisterMigration(IDSMMigration migration)
    {
        if (s_manager != null)
            throw new InvalidOperationException("DSM: migrations must be registered before the first DSM access — the manager is already built.");
        s_migrations.Add(migration);
    }

    /// <summary>Clears all registered migrations and drops the current manager so the next access rebuilds with the cleared set.</summary>
    public static void ClearMigrations()
    {
        s_migrations.Clear();
        if (s_manager != null)
            Application.quitting -= s_manager.SaveActiveSlot;
        s_manager = null;
    }

    // --- Slot management ---

    /// <summary>Switches the active slot to <paramref name="name"/>, loading its data. Creates the slot if it does not exist.</summary>
    public static void UseSlot(string name) => Manager.UseSlot(name);

    /// <summary>Returns the <see cref="DSMSlot"/> for <paramref name="name"/>, creating it if it does not exist.</summary>
    public static DSMSlot GetSlot(string name) => Manager.GetSlot(name);

    /// <summary>Deletes the save files for <paramref name="name"/> and removes it from the in-memory cache.</summary>
    public static void DeleteSlot(string name) => Manager.DeleteSlot(name);

    /// <summary>Returns the names of all save slots found on disk.</summary>
    public static string[] GetAllSlots() => Manager.GetAllSlots();

    /// <summary>The save version the registered migrations bring a save up to.</summary>
    public static int CurrentSaveVersion => Manager.CurrentVersion;

    // --- Core get / set ---

    /// <summary>Sets <paramref name="value"/> for <paramref name="key"/> in the active slot. Triggers AutoSave debounce if enabled.</summary>
    public static void Set<T>(string key, T value) where T : notnull =>
        Manager.ActiveSlot.Set(key, value);

    /// <summary>Returns the value for <paramref name="key"/> in the active slot, or <paramref name="defaultValue"/> if the key does not exist.</summary>
    public static T Get<T>(string key, T defaultValue) =>
        Manager.ActiveSlot.Get(key, defaultValue);

    /// <summary>Returns true if <paramref name="key"/> exists in the active slot.</summary>
    public static bool Has(string key) => Manager.ActiveSlot.Has(key);

    /// <summary>Removes <paramref name="key"/> from the active slot.</summary>
    public static void Delete(string key) => Manager.ActiveSlot.Delete(key);

    /// <summary>Removes all keys from the active slot.</summary>
    public static void Clear() => Manager.ActiveSlot.Clear();

    // --- Save / Load ---

    /// <summary>Synchronously saves the active slot to disk.</summary>
    public static void Save() => Manager.ActiveSlot.Save();

    /// <summary>Synchronously loads the active slot from disk, seeding defaults if no file exists.</summary>
    public static void Load() => Manager.ActiveSlot.Load();

    /// <summary>Asynchronously saves the active slot to disk.</summary>
    public static UniTask SaveAsync() => Manager.ActiveSlot.SaveAsync();

    /// <summary>Asynchronously loads the active slot from disk, seeding defaults if no file exists.</summary>
    public static UniTask LoadAsync() => Manager.ActiveSlot.LoadAsync();

    // --- Key rotation ---

    /// <summary>
    /// Re-encrypts every encrypted slot from the current key to <paramref name="newKey"/> and
    /// commits the new key only after every slot succeeds. Staging (decrypt-old/re-encrypt-new)
    /// is all-or-nothing: any failure leaves every slot readable with the old key and the config
    /// key unchanged. The commit burst that follows is journal-backed, so an interruption there
    /// is completed automatically by the next <see cref="DSMSlotManager"/> construction — no
    /// slot is ever left unreadable.
    /// </summary>
    public static UniTask RotateEncryptionKeyAsync(string newKey) => Manager.RotateEncryptionKeyAsync(newKey);

    // --- Change notification ---

    /// <summary>Returns an async stream that emits the current value immediately, then emits each subsequent value when the key is updated via <see cref="Set{T}"/>.</summary>
    public static IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) =>
        Manager.ActiveSlot.WatchAsync<T>(key);

    // --- Lazy initialization ---

    private static DSMSlotManager Initialize()
    {
        var config = Resources.Load<DSMConfig>("DSMConfig")
                     ?? ScriptableObject.CreateInstance<DSMConfig>();

        var manager = new DSMSlotManager(config, new DSMMigrationRunner(s_migrations));
        Application.quitting += manager.SaveActiveSlot;
        return manager;
    }
}
