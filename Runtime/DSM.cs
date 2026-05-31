#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

public static class DSM
{
    private static DSMSlotManager? s_manager;

    private static DSMSlotManager Manager => s_manager ??= Initialize();

    /// <summary>Overrides the auto-loaded DSMConfig. Call before any other DSM method to use a custom config.</summary>
    public static void Configure(DSMConfig config)
    {
        if (s_manager != null)
            Application.quitting -= s_manager.SaveActiveSlot;
        s_manager = new DSMSlotManager(config);
        Application.quitting += s_manager.SaveActiveSlot;
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

    // --- Change notification ---

    /// <summary>Returns an async stream that emits the current value immediately, then emits each subsequent value when the key is updated via <see cref="Set{T}"/>.</summary>
    public static IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) =>
        Manager.ActiveSlot.WatchAsync<T>(key);

    // --- Lazy initialization ---

    private static DSMSlotManager Initialize()
    {
        var config = Resources.Load<DSMConfig>("DSMConfig")
                     ?? ScriptableObject.CreateInstance<DSMConfig>();

        var manager = new DSMSlotManager(config);
        Application.quitting += manager.SaveActiveSlot;
        return manager;
    }
}
