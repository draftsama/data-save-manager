#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;

namespace OperatorConsole.Editor
{
    internal static class OCMenu
    {
        private const string ConfigPath = "Assets/Resources/DSMConfig.asset";

        /// <summary>Loads the Resources config asset, creating it (and its folder) first if it doesn't exist yet. Pings/selects it either way.</summary>
        internal static OCConfig CreateConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<OCConfig>(ConfigPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return existing;
            }

            var dir = Path.GetDirectoryName(ConfigPath)!;
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var config = ScriptableObject.CreateInstance<OCConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
            Selection.activeObject = config;
            return config;
        }
    }
}
