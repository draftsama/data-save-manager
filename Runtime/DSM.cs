#nullable enable

using Cysharp.Threading.Tasks;
using DataSaveManager;
using UnityEngine;

/// <summary>Global static facade over the active <see cref="DSMStore"/>.</summary>
public static class DSM
{
    private static DSMStore? s_store;
    private static bool s_quittingHooked;

    /// <summary>The active store, created lazily from the Resources config on first access.</summary>
    public static DSMStore Store => s_store ??= CreateDefault();

    /// <summary>Overrides the active config: saves and disposes any existing store, then loads a new one.</summary>
    public static void Configure(DSMConfig config)
    {
        if (s_store != null)
        {
            if (s_store.IsDirty) s_store.Save();
            s_store.Dispose();
        }

        s_store = new DSMStore(config);
        s_store.Load();
        HookQuitting();
    }

    /// <summary>Returns the value for <paramref name="key"/>, or the Entry's Default if never overridden.</summary>
    public static T Get<T>(string key) => Store.Get<T>(key);

    /// <summary>Returns the value for <paramref name="key"/>, or <paramref name="fallback"/> if there is neither an Override nor a usable Default.</summary>
    public static T Get<T>(string key, T fallback) => Store.Get(key, fallback);

    /// <summary>Sets an Override for <paramref name="key"/>. Triggers AutoSave debounce if enabled.</summary>
    public static void Set<T>(string key, T value) => Store.Set(key, value);

    /// <summary>Returns true if <paramref name="key"/> has an explicit Override.</summary>
    public static bool HasOverride(string key) => Store.HasOverride(key);

    /// <summary>Removes the Override for <paramref name="key"/>, falling back to its Default.</summary>
    public static void Reset(string key) => Store.Reset(key);

    /// <summary>Removes every Override, falling back to Defaults.</summary>
    public static void ResetAll() => Store.ResetAll();

    /// <summary>Synchronously saves all Overrides to the Save File.</summary>
    public static void Save() => Store.Save();

    /// <summary>Synchronously loads Overrides from the Save File.</summary>
    public static void Load() => Store.Load();

    /// <summary>Returns an async stream that emits the current value immediately, then every subsequent change.</summary>
    public static IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) => Store.WatchAsync<T>(key);

    /// <summary>The active config.</summary>
    public static DSMConfig Config => Store.Config;

    /// <summary>The resolved path of the Save File.</summary>
    public static string SaveFilePath => Store.SaveFilePath;

    private static DSMStore CreateDefault()
    {
        var config = Resources.Load<DSMConfig>("DSMConfig");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<DSMConfig>();
            Debug.LogWarning("DSM: no DSMConfig found in Resources — using an empty config.");
        }

        var store = new DSMStore(config);
        store.Load();
        HookQuitting();
        return store;
    }

    private static void HookQuitting()
    {
        if (s_quittingHooked) return;
        s_quittingHooked = true;
        Application.quitting += OnQuitting;
    }

    private static void OnQuitting()
    {
        if (s_store?.IsDirty == true) s_store.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Enter Play Mode without domain reload keeps edit-mode statics alive; persist before dropping them.
        if (s_store?.IsDirty == true) s_store.Save();
        s_store?.Dispose();
        s_store = null;
    }
}
