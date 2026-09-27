#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    internal static class DSMMenu
    {
        private const string ConfigPath = "Assets/Resources/DSMConfig.asset";

        [MenuItem("DSM/Create Config Asset")]
        private static void CreateConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DSMConfig>(ConfigPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return;
            }

            var dir = Path.GetDirectoryName(ConfigPath)!;
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var config = ScriptableObject.CreateInstance<DSMConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
            Selection.activeObject = config;
        }

        [MenuItem("DSM/Open Save Folder")]
        private static void OpenSaveFolder()
        {
            var config = AssetDatabase.LoadAssetAtPath<DSMConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"DSM: no DSMConfig asset found at '{ConfigPath}'. Create one via DSM/Create Config Asset first.");
                return;
            }

            var path = DSMPaths.ResolveSaveFilePath(config);
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
