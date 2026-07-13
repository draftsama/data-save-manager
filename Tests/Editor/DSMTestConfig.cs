#nullable enable

using System.Reflection;
using UnityEngine;

/// <summary>
/// Test-only fixture builder for <see cref="DSMConfig"/>. Sets private
/// <c>[SerializeField]</c> backing fields via reflection since the public API
/// intentionally exposes no setters (config is authored as a ScriptableObject asset).
/// </summary>
public static class DSMTestConfig
{
    public static DSMConfig Create(
        bool autoSave = false,
        float autoSaveDebounce = 0.05f,
        bool encrypt = false,
        string? savePath = null,
        string? encryptionKey = null)
    {
        var config = ScriptableObject.CreateInstance<DSMConfig>();

        SetField(config, "_autoSave", autoSave);
        SetField(config, "_autoSaveDebounce", autoSaveDebounce);
        SetField(config, "_encrypt", encrypt);
        if (savePath != null)
            SetField(config, "_savePath", savePath);
        if (encryptionKey != null)
            config.SetEncryptionKey(encryptionKey);

        return config;
    }

    private static void SetField(DSMConfig config, string fieldName, object value)
    {
        var field = typeof(DSMConfig).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(config, value);
    }
}
