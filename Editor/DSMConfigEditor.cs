#nullable enable
#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DSMConfig))]
public sealed class DSMConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Open Save Folder"))
            OpenSaveFolder();

        if (GUILayout.Button("Clear All Saves"))
            ClearAllSaves();

        if (GUILayout.Button("Test Encrypt / Decrypt"))
            TestEncryption();
    }

    [MenuItem("DSM/Create Config Asset")]
    private static void CreateConfigAsset()
    {
        Directory.CreateDirectory("Assets/Resources");
        var asset = CreateInstance<DSMConfig>();
        AssetDatabase.CreateAsset(asset, "Assets/Resources/DSMConfig.asset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
    }

    private void OpenSaveFolder()
    {
        var config = (DSMConfig)target;
        var path = string.IsNullOrEmpty(config.SavePath)
            ? Path.Combine(Application.persistentDataPath, "DSM")
            : config.SavePath;
        Directory.CreateDirectory(path);
        EditorUtility.RevealInFinder(path);
    }

    private void ClearAllSaves()
    {
        if (!EditorUtility.DisplayDialog(
                "Clear All Saves",
                "Delete all DSM save files? This cannot be undone.",
                "Delete",
                "Cancel"))
            return;

        var config = (DSMConfig)target;
        var dir = string.IsNullOrEmpty(config.SavePath)
            ? Path.Combine(Application.persistentDataPath, "DSM")
            : config.SavePath;

        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir))
            File.Delete(file);

        Debug.Log("DSM: All save files deleted.");
    }

    private void TestEncryption()
    {
        var config = (DSMConfig)target;
        const string testData = "{\"test\":\"hello DSM\"}";

        try
        {
            var encrypted = DSMEncryptor.Encrypt(testData, config.EncryptionKey);
            var decrypted = DSMEncryptor.Decrypt(encrypted, config.EncryptionKey);
            var passed = decrypted == testData;

            Debug.Log(passed
                ? "DSM: Encryption test passed."
                : $"DSM: Encryption test FAILED. Expected: {testData}, Got: {decrypted}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DSM: Encryption test threw exception: {e.Message}");
        }
    }
}

#endif
