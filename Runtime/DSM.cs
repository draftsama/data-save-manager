#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OperatorConsole;

/// <summary>The DataSaveManager 3.x facade, kept for one major version. Every member forwards to <see cref="OC"/>; removed in 5.0.</summary>
[Obsolete("DSM is now OC (OperatorConsole 4.0); removed in 5.0.")]
public static class DSM
{
    public static OCStore Store => OC.Store;
    public static OCCommandRegistry Commands => OC.Commands;
    public static OCPanelRegistry Panels => OC.Panels;
    public static OCConfig Config => OC.Config;
    public static string SaveFilePath => OC.SaveFilePath;

    public static void Configure(OCConfig config) => OC.Configure(config);
    public static T Get<T>(string key) => OC.Get<T>(key);
    public static T Get<T>(string key, T fallback) => OC.Get(key, fallback);
    public static void Set<T>(string key, T value) => OC.Set(key, value);
    public static bool HasOverride(string key) => OC.HasOverride(key);
    public static void Reset(string key) => OC.Reset(key);
    public static void ResetAll() => OC.ResetAll();
    public static void ResetExposedEntries() => OC.ResetExposedEntries();
    public static void ResetExposedEntries(IReadOnlyList<string> panelAudienceIds) => OC.ResetExposedEntries(panelAudienceIds);
    public static void Save() => OC.Save();
    public static void Load() => OC.Load();
    public static IUniTaskAsyncEnumerable<T> WatchAsync<T>(string key) => OC.WatchAsync<T>(key);
}
