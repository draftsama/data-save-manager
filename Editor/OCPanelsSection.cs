#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OperatorConsole.Editor
{
    /// <summary>The Operator Console's Runtime Panels section: edits the Config's Panel Definitions.</summary>
    internal sealed class OCPanelsSection
    {
        private static readonly Color DuplicateNameColor = new(0.65f, 0.28f, 0.28f);
        private static readonly GUIContent AudiencesLabel = new("Audiences", "Exposed Entries this Panel shows: those naming any of these Audiences, plus those naming none. None selected: every Exposed Entry.");
        internal static readonly GUIContent TabLabel = new("Runtime Panels", "Each enabled Panel Definition becomes one Runtime Panel when the app starts.");

        private readonly List<bool> _expanded = new();
        private readonly List<string> _newPasscodes = new();
        private Action? _deferred;

        /// <summary>Draws the section; returns true when a Panel Definition changed.</summary>
        public bool Draw(SerializedObject configSo, OCConfig config, string[] audienceNames)
        {
            var panelsProp = configSo.FindProperty("_panels");
            Sync(panelsProp.arraySize);
            var duplicates = DuplicateNames(panelsProp);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (panelsProp.arraySize == 0)
                    EditorGUILayout.HelpBox("No Panel Definitions: the app shows no Runtime Panel.", MessageType.Info);

                for (var i = 0; i < panelsProp.arraySize; i++)
                    DrawDefinition(panelsProp, i, config, audienceNames, duplicates);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Add Panel", GUILayout.Width(110)))
                        _deferred = () => AddDefinition(panelsProp, UniqueName(panelsProp, "Panel"), null);
                    if (GUILayout.Button(new GUIContent("Import Scene Panels", "Create a Panel Definition from each Runtime Panel placed in the open scenes."), GUILayout.Width(150)))
                        _deferred = () => ShowImportResult(ImportScenePanels(panelsProp));
                }
            }
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>Runs an add, remove or import picked during Draw; adding or removing mid-draw would break the layout. Returns true when one ran.</summary>
        public bool RunDeferred()
        {
            if (_deferred == null) return false;
            var edit = _deferred;
            _deferred = null;
            edit();
            return true;
        }

        private void DrawDefinition(SerializedProperty panelsProp, int index, OCConfig config, string[] audienceNames, HashSet<string> duplicates)
        {
            var definition = panelsProp.GetArrayElementAtIndex(index);
            var nameProp = definition.FindPropertyRelative("_name");
            var enabledProp = definition.FindPropertyRelative("_enabled");

            using (new EditorGUILayout.HorizontalScope())
            {
                var title = enabledProp.boolValue ? nameProp.stringValue : $"{nameProp.stringValue} (disabled)";
                _expanded[index] = EditorGUILayout.Foldout(_expanded[index], title, true);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    var name = nameProp.stringValue;
                    _deferred = () =>
                    {
                        if (!EditorUtility.DisplayDialog("Remove Panel Definition", $"Remove Panel Definition '{name}'? Its Passcode is lost.", "Remove", "Cancel")) return;
                        panelsProp.DeleteArrayElementAtIndex(index);
                        _expanded.RemoveAt(index);
                        _newPasscodes.RemoveAt(index);
                    };
                }
            }
            if (!_expanded[index]) return;

            using (new EditorGUI.IndentLevelScope())
            {
                var isDuplicate = duplicates.Contains(nameProp.stringValue);
                var previousColor = GUI.backgroundColor;
                if (isDuplicate) GUI.backgroundColor = DuplicateNameColor;
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Name", "Code finds this Runtime Panel by this name: OC.Panels.Get(name)."));
                GUI.backgroundColor = previousColor;
                if (isDuplicate)
                    EditorGUILayout.HelpBox("Another Panel Definition has this name; OC.Panels.Get returns only the first.", MessageType.Error);

                EditorGUILayout.PropertyField(enabledProp, new GUIContent("Enabled", "Disabled Panel Definitions create no Runtime Panel."));
                var prefabProp = definition.FindPropertyRelative("_prefab");
                EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));
                if (prefabProp.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("No prefab: no Runtime Panel is created for this Panel Definition.", MessageType.Error);
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_sortOrder"), new GUIContent("Sort Order"));

                DrawAudiences(definition.FindPropertyRelative("_audienceIds"), config, audienceNames);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Open Triggers", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_hotkeyEnabled"), new GUIContent("Hotkey Enabled"));
                var toggleKeyProp = definition.FindPropertyRelative("_toggleKey");
                EditorGUILayout.PropertyField(toggleKeyProp, new GUIContent("Toggle Key"));
#if ENABLE_INPUT_SYSTEM
                if (!OCKeyMap.TryGetKey((KeyCode)toggleKeyProp.intValue, out _))
                    EditorGUILayout.HelpBox("Not a keyboard key: the hotkey does nothing with the Input System.", MessageType.Error);
#endif
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_cornerTap"), new GUIContent("Corner Tap"), true);
                if (index < config.Panels.Count)
                {
                    var conflicts = TriggerConflicts(config, index, false);
                    if (conflicts.Count > 0)
                        EditorGUILayout.HelpBox($"Shares an Open Trigger with {string.Join(", ", conflicts)}: one use opens both panels.", MessageType.Warning);
                    var lockedConflicts = TriggerConflicts(config, index, true);
                    if (lockedConflicts.Count > 0)
                        EditorGUILayout.HelpBox($"Has a Panel Lock and shares an Open Trigger with {string.Join(", ", lockedConflicts)}, which also has one: only one Lock Screen shows at a time, so that use opens only the first of them in this list.", MessageType.Warning);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Panel Lock", EditorStyles.boldLabel);
                var newPasscode = _newPasscodes[index];
                OCPasscodeEditing.Draw(definition.FindPropertyRelative("_passcode"), ref newPasscode, HasLockScreen(prefabProp.objectReferenceValue));
                _newPasscodes[index] = newPasscode;

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_startVisible"), new GUIContent("Start Visible"));
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_saveButtonMode"), new GUIContent("Save Button Mode"));
                EditorGUILayout.PropertyField(definition.FindPropertyRelative("_layout"), new GUIContent("Layout"), true);
            }
            EditorGUILayout.Space(4);
        }

        private static void DrawAudiences(SerializedProperty idsProp, OCConfig config, string[] audienceNames)
        {
            if (config.Audiences.Count == 0) return;
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            OCAudienceEditing.DrawMaskField(rect, AudiencesLabel, idsProp, config, audienceNames);
            if (idsProp.arraySize == 0)
                EditorGUILayout.HelpBox("This Panel serves no Audience, so it shows every Exposed Entry, including ones meant for other Audiences.", MessageType.Warning);
        }

        private static bool HasLockScreen(Object? prefab)
        {
            if (prefab == null) return false;
            using var so = new SerializedObject(prefab);
            return so.FindProperty("_lockScreen")?.objectReferenceValue != null;
        }

        private static List<string> TriggerConflicts(OCConfig config, int index, bool lockedOnly)
        {
            var conflicts = new List<string>();
            var self = config.Panels[index];
            if (!self.Enabled || (lockedOnly && !self.Passcode.IsSet)) return conflicts;
            for (var i = 0; i < config.Panels.Count; i++)
            {
                if (i == index) continue;
                var other = config.Panels[i];
                if (!other.Enabled || (lockedOnly && !other.Passcode.IsSet)) continue;
                var sameKey = self.HotkeyEnabled && other.HotkeyEnabled && self.ToggleKey == other.ToggleKey;
                var sameCorner = self.CornerTap.Enabled && other.CornerTap.Enabled && self.CornerTap.Corner == other.CornerTap.Corner;
                if (sameKey || sameCorner) conflicts.Add($"'{other.Name}'");
            }
            return conflicts;
        }

        private void Sync(int count)
        {
            while (_expanded.Count < count) _expanded.Add(true);
            while (_newPasscodes.Count < count) _newPasscodes.Add(string.Empty);
            if (_expanded.Count > count) _expanded.RemoveRange(count, _expanded.Count - count);
            if (_newPasscodes.Count > count) _newPasscodes.RemoveRange(count, _newPasscodes.Count - count);
        }

        private static HashSet<string> DuplicateNames(SerializedProperty panelsProp)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < panelsProp.arraySize; i++)
            {
                var name = panelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue;
                if (!seen.Add(name)) duplicates.Add(name);
            }
            return duplicates;
        }

        private static string UniqueName(SerializedProperty panelsProp, string baseName)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < panelsProp.arraySize; i++)
                names.Add(panelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
            if (!names.Contains(baseName)) return baseName;
            for (var n = 2; ; n++)
                if (!names.Contains($"{baseName} {n}")) return $"{baseName} {n}";
        }

        private static SerializedProperty AddDefinition(SerializedProperty panelsProp, string name, OCRuntimePanel? prefab)
        {
            var index = panelsProp.arraySize;
            panelsProp.InsertArrayElementAtIndex(index);
            var definition = panelsProp.GetArrayElementAtIndex(index);
            // InsertArrayElementAtIndex copies the previous element, so every field is set here.
            definition.boxedValue = new OCPanelDefinition();
            definition.FindPropertyRelative("_name").stringValue = name;
            definition.FindPropertyRelative("_prefab").objectReferenceValue = prefab != null ? prefab : DefaultPrefab();
            return definition;
        }

        private static OCRuntimePanel? DefaultPrefab()
        {
            var folder = OCPanelPrefabBuilder.ResolvePrefabFolder();
            return folder == null ? null : AssetDatabase.LoadAssetAtPath<OCRuntimePanel>($"{folder}/OCRuntimePanel.prefab");
        }

        /// <summary>Appends one Panel Definition per Runtime Panel placed in the open scenes; returns a line per panel imported. The scenes are not changed.</summary>
        internal static List<string> ImportScenePanels(SerializedProperty panelsProp)
        {
            var panels = Object.FindObjectsByType<OCRuntimePanel>(FindObjectsInactive.Include);
            var imported = new List<string>();
            foreach (var panel in panels)
            {
                if (!panel.gameObject.scene.IsValid()) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(panel);
                var definition = AddDefinition(panelsProp, UniqueName(panelsProp, panel.gameObject.name), source);

                using var panelSo = new SerializedObject(panel);
                Copy(panelSo, "_legacyAudienceIds", definition, "_audienceIds");
                Copy(panelSo, "_legacySaveButtonMode", definition, "_saveButtonMode");
                Copy(panelSo, "_legacyHotkeyEnabled", definition, "_hotkeyEnabled");
                Copy(panelSo, "_legacyToggleKey", definition, "_toggleKey");
                Copy(panelSo, "_legacyCornerTap", definition, "_cornerTap");
                Copy(panelSo, "_legacyPasscode", definition, "_passcode");
                Copy(panelSo, "_legacyStartVisible", definition, "_startVisible");
                Copy(panelSo, "_legacyLayout", definition, "_layout");
                if (panel.TryGetComponent<Canvas>(out var canvas))
                    definition.FindPropertyRelative("_sortOrder").intValue = canvas.sortingOrder;
                imported.Add($"'{panel.gameObject.name}' in {panel.gameObject.scene.name}");
            }
            return imported;
        }

        private static void ShowImportResult(List<string> imported)
        {
            EditorUtility.DisplayDialog(
                "Import Scene Panels",
                imported.Count == 0
                    ? "No Runtime Panel is placed in the open scenes."
                    : $"Created a Panel Definition from:\n\n- {string.Join("\n- ", imported)}\n\nRemove these panels from their scenes and save the scenes; until then they stay hidden in Play mode.",
                "OK");
        }

        private static void Copy(SerializedObject from, string fromPath, SerializedProperty to, string toPath)
        {
            var source = from.FindProperty(fromPath);
            var target = to.FindPropertyRelative(toPath);
            if (source == null || target == null) return;
            if (!source.isArray)
            {
                target.boxedValue = source.boxedValue;
                return;
            }
            // boxedValue can't read arrays, so their elements are copied one by one.
            target.arraySize = source.arraySize;
            for (var i = 0; i < source.arraySize; i++)
                target.GetArrayElementAtIndex(i).boxedValue = source.GetArrayElementAtIndex(i).boxedValue;
        }
    }
}
