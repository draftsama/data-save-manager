#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

public static class DSM
{
    private static DSMSlotManager? s_manager;

    private static DSMSlotManager Manager => s_manager ??= Initialize();

    // Optional: override the auto-loaded config at any time
    public static void Configure(DSMConfig config)
    {
        if (s_manager != null)
            Application.quitting -= s_manager.SaveActiveSlot;
        s_manager = new DSMSlotManager(config);
        Application.quitting += s_manager.SaveActiveSlot;
    }

    // --- Slot management ---

    public static void UseSlot(string name) => Manager.UseSlot(name);
    public static DSMSlot GetSlot(string name) => Manager.GetSlot(name);
    public static void DeleteSlot(string name) => Manager.DeleteSlot(name);
    public static string[] GetAllSlots() => Manager.GetAllSlots();

    // --- Core get / set ---

    public static void Set<T>(string key, T value) where T : notnull =>
        Manager.ActiveSlot.Set(key, value);

    public static T Get<T>(string key, T defaultValue) =>
        Manager.ActiveSlot.Get(key, defaultValue);

    public static bool Has(string key) => Manager.ActiveSlot.Has(key);
    public static void Delete(string key) => Manager.ActiveSlot.Delete(key);
    public static void Clear() => Manager.ActiveSlot.Clear();

    // --- Save / Load ---

    public static void Save() => Manager.ActiveSlot.Save();
    public static void Load() => Manager.ActiveSlot.Load();
    public static UniTask SaveAsync() => Manager.ActiveSlot.SaveAsync();
    public static UniTask LoadAsync() => Manager.ActiveSlot.LoadAsync();

    // --- Change notification ---

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
