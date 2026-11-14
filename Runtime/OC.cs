#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OperatorConsole;
using UnityEngine;

/// <summary>Global static facade over the active <see cref="OCStore"/>.</summary>
public static class OC
{
    private static OCStore? s_store;
    private static OCCommandRegistry? s_commands;
    private static OCPanelRegistry? s_panels;
    private static OCLog? s_log;
    private static OCPerformance? s_performance;
    private static bool s_quittingHooked;

    /// <summary>The active store, created lazily from the Resources config on first access.</summary>
    public static OCStore Store => s_store ??= CreateDefault();

    /// <summary>The Registered Commands the Runtime Panel lists. Owners call <c>Register(this)</c> in OnEnable and <c>Unregister(this)</c> in OnDisable.</summary>
    public static OCCommandRegistry Commands => s_commands ??= new OCCommandRegistry(() => Store.Config);

    /// <summary>The Runtime Panels created from the Config's Panel Definitions when the app started.</summary>
    public static OCPanelRegistry Panels => s_panels ??= new OCPanelRegistry();

    /// <summary>The messages the app wrote to Unity's log, kept from app start when an enabled Panel Definition shows the Log tab.</summary>
    public static OCLog Log => s_log ??= new OCLog();

    /// <summary>Live frame rate, frame time and memory, measured when an enabled Panel Definition shows the Performance tab.</summary>
    public static OCPerformance Performance => s_performance ??= new OCPerformance();

    /// <summary>Overrides the active config: saves and disposes any existing store, then loads a new one.</summary>
    public static void Configure(OCConfig config)
    {
        if (s_store != null)
        {
            if (s_store.IsDirty) s_store.Save();
            s_store.Dispose();
        }

        s_store = new OCStore(config);
        s_store.Load();
        HookQuitting();

        OCDiagnostics.ApplyLogCapture(config, Log);
        if (s_panels != null && s_panels.HasSpawned)
        {
            OCDiagnostics.StartTicking(config, Log, Performance);
            s_panels.Respawn(config);
        }
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

    /// <summary>Removes the Overrides of Exposed Entries only, as the Runtime Panel's Reset does; other Overrides survive.</summary>
    public static void ResetExposedEntries() => Store.ResetExposedEntries();

    /// <summary>Removes the Overrides of the Exposed Entries a Runtime Panel serving <paramref name="panelAudienceIds"/> shows; other Overrides survive.</summary>
    public static void ResetExposedEntries(IReadOnlyList<string> panelAudienceIds) => Store.ResetExposedEntries(panelAudienceIds);

    /// <summary>Synchronously saves all Overrides to the Save File.</summary>
    public static void Save() => Store.Save();

    /// <summary>Synchronously loads Overrides from the Save File.</summary>
    public static void Load() => Store.Load();

    /// <summary>Returns an async stream that emits the current value immediately, then every subsequent change.</summary>
    public static IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) => Store.WatchAsync<T>(key);

    /// <summary>The active config.</summary>
    public static OCConfig Config => Store.Config;

    /// <summary>The resolved path of the Save File.</summary>
    public static string SaveFilePath => Store.SaveFilePath;

    private static OCStore CreateDefault()
    {
        var config = Resources.Load<OCConfig>("DSMConfig");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<OCConfig>();
            Debug.LogWarning("OC: no OCConfig named DSMConfig found in Resources — using an empty config.");
        }

        var store = new OCStore(config);
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
        s_commands?.Clear();
        s_commands = null;
        s_panels?.Clear();
        s_panels = null;
        OCDiagnostics.Shutdown(s_log, s_performance);
        s_log = null;
        s_performance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartLogCapture() => OCDiagnostics.ApplyLogCapture(Store.Config, Log);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SpawnPanels()
    {
        if (Panels.HasSpawned) return;
        OCDiagnostics.StartTicking(Store.Config, Log, Performance);
        Panels.SpawnAll(Store.Config);
    }
}
