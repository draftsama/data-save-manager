#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Editor helpers for picking Audiences by name while storing their ids.</summary>
    internal static class DSMAudienceEditing
    {
        // EditorGUI.MaskField packs the selection into an int.
        public const int MaxAudiences = 32;

        public static string[] Names(DSMConfig config)
        {
            var names = new string[config.Audiences.Count];
            for (var i = 0; i < names.Length; i++)
            {
                var name = config.Audiences[i].Name;
                names[i] = string.IsNullOrWhiteSpace(name) ? $"(unnamed {i + 1})" : name;
            }
            return names;
        }

        public static void DrawMaskField(Rect rect, GUIContent label, SerializedProperty idsProp, DSMConfig config, string[] names)
        {
            var mask = 0;
            for (var i = 0; i < idsProp.arraySize; i++)
            {
                var index = IndexOf(config, idsProp.GetArrayElementAtIndex(i).stringValue);
                if (index >= 0) mask |= 1 << index;
            }

            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.MaskField(rect, label, mask, names);
            if (!EditorGUI.EndChangeCheck()) return;

            var ids = new List<string>();
            for (var i = 0; i < config.Audiences.Count; i++)
                if ((next & (1 << i)) != 0) ids.Add(config.Audiences[i].Id);

            idsProp.arraySize = ids.Count;
            for (var i = 0; i < ids.Count; i++)
                idsProp.GetArrayElementAtIndex(i).stringValue = ids[i];
        }

        private static int IndexOf(DSMConfig config, string id)
        {
            for (var i = 0; i < config.Audiences.Count; i++)
                if (config.Audiences[i].Id == id) return i;
            return -1;
        }

        /// <summary>Where an Audience is still named: Entries and Panel Definitions in the Config, plus Runtime Panels from before Panel Definitions in prefab / scene files under Assets.</summary>
        public static List<string> FindUsers(DSMConfig config, string audienceId)
        {
            var users = new List<string>();
            foreach (var key in DSMEntryRules.EntriesNamingAudience(config, audienceId))
                users.Add($"Entry '{key}'");

            foreach (var panel in config.Panels)
                if (Contains(panel.AudienceIds, audienceId)) users.Add($"Panel Definition '{panel.Name}'");

            // Closed scenes and prefabs aren't loaded, so their files are searched for the id as text.
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            foreach (var pattern in new[] { "*.prefab", "*.unity" })
            {
                foreach (var file in Directory.EnumerateFiles(Application.dataPath, pattern, SearchOption.AllDirectories))
                {
                    if (!File.ReadAllText(file).Contains(audienceId, StringComparison.Ordinal)) continue;
                    var relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    users.Add($"Runtime Panel in {relative}");
                }
            }
            return users;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;
            return false;
        }
    }
}
