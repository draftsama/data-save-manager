#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Edits a DSMConfig's entry definitions and, through <see cref="DSM"/>, the live store's values.</summary>
    internal sealed class DSMManagerWindow : EditorWindow
    {
        private static readonly Color DuplicateKeyColor = new(0.65f, 0.28f, 0.28f);

        private DSMConfig? _config;
        private SerializedObject? _configSo;
        private string _search = string.Empty;
        private Vector2 _entriesScroll;
        private bool _settingsFoldout = true;
        private string _newKey = string.Empty;
        private DSMDataType _newType = DSMDataType.String;

        [MenuItem("DSM/Open Manager")]
        internal static void Open()
        {
            var window = GetWindow<DSMManagerWindow>("DSM Manager");
            window.minSize = new Vector2(640, 480);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Reload(fromDisk: false);
        }

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        private void OnPlayModeStateChanged(PlayModeStateChange _) => Repaint();

        // Values live on DSM.Store, which changes outside this window's own GUI events (Play mode, other code) —
        // the default ~10 Hz OnInspectorUpdate tick keeps the window in sync without a bespoke poller.
        private void OnInspectorUpdate() => Repaint();

        // Opening the window must not discard unsaved in-memory values (e.g. mid Play mode); only the
        // explicit Reload button re-reads the Save File.
        private void Reload(bool fromDisk)
        {
            _config = Resources.Load<DSMConfig>("DSMConfig");
            _configSo = _config != null ? new SerializedObject(_config) : null;
            if (_config != null && fromDisk) DSM.Load();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_config == null || _configSo == null)
            {
                DrawNoConfig();
                return;
            }

            _configSo.Update();

            var duplicates = DSMEntryRules.DuplicateKeys(_config);
            var valuesEditable = DSM.Store.Config == _config;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Config", _config, typeof(DSMConfig), false);

            if (!valuesEditable)
                EditorGUILayout.HelpBox(
                    "DSM.Store is configured with a different DSMConfig asset. This window only edits the Resources config's values, so the Value column is disabled.",
                    MessageType.Warning);

            if (duplicates.Count > 0)
                EditorGUILayout.HelpBox($"Duplicate keys: {string.Join(", ", duplicates)}", MessageType.Error);

            EditorGUILayout.Space();
            DrawSettings();

            EditorGUILayout.Space();
            DrawEntries(duplicates, valuesEditable);

            EditorGUILayout.Space();
            DrawAddEntry();

            EditorGUILayout.Space();
            DrawFooter();

            _configSo.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    Reload(fromDisk: true);

                GUILayout.FlexibleSpace();
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }
        }

        private void DrawNoConfig()
        {
            EditorGUILayout.HelpBox("No DSMConfig found in Resources. Create one to begin.", MessageType.Warning);
            if (GUILayout.Button("Create Config Asset", GUILayout.Height(26)))
            {
                DSMMenu.CreateConfigAsset();
                Reload(fromDisk: false);
            }
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Settings", true);
            if (!_settingsFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_configSo!.FindProperty("_autoSave"), new GUIContent("AutoSave"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_autoSaveDebounce"), new GUIContent("AutoSave Debounce"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_saveDirectory"), new GUIContent("Save Directory"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_fileName"), new GUIContent("File Name"));

                EditorGUILayout.Space(4);
                var path = DSMPaths.ResolveSaveFilePath(_config!);
                EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(18));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                            EditorUtility.RevealInFinder(dir);
                        }
                    }

                    if (GUILayout.Button("Delete Save File", GUILayout.Width(120)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Save File", $"Delete '{path}'? This cannot be undone.", "Delete", "Cancel"))
                        {
                            if (File.Exists(path)) File.Delete(path);
                            DSM.Load();
                        }
                    }
                }
            }
        }

        private void DrawEntries(IReadOnlyCollection<string> duplicates, bool valuesEditable)
        {
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);

            var entriesProp = _configSo!.FindProperty("_entries");
            var filter = _search.Trim();
            var pendingRemoveIndex = -1;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_entriesScroll, GUILayout.ExpandHeight(true)))
            {
                _entriesScroll = scroll.scrollPosition;

                for (var i = 0; i < entriesProp.arraySize; i++)
                {
                    var elementProp = entriesProp.GetArrayElementAtIndex(i);
                    var keyProp = elementProp.FindPropertyRelative("_key");
                    var typeProp = elementProp.FindPropertyRelative("_type");
                    var labelProp = elementProp.FindPropertyRelative("_label");
                    var exposedProp = elementProp.FindPropertyRelative("_exposed");
                    var defaultJsonProp = elementProp.FindPropertyRelative("_defaultJson");

                    var key = keyProp.stringValue;
                    if (filter.Length > 0 &&
                        key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        labelProp.stringValue.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var isDuplicate = duplicates.Contains(key);

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        var priorColor = GUI.backgroundColor;
                        if (isDuplicate) GUI.backgroundColor = DuplicateKeyColor;
                        keyProp.stringValue = EditorGUILayout.TextField(keyProp.stringValue, GUILayout.Width(110));
                        GUI.backgroundColor = priorColor;

                        var currentType = (DSMDataType)typeProp.enumValueIndex;
                        var newType = (DSMDataType)EditorGUILayout.EnumPopup(currentType, GUILayout.Width(80));
                        if (newType != currentType)
                        {
                            typeProp.enumValueIndex = (int)newType;
                            defaultJsonProp.stringValue = DSMEntryDefinition.DefaultJsonFor(newType);
                        }

                        labelProp.stringValue = EditorGUILayout.TextField(labelProp.stringValue, GUILayout.Width(100));
                        exposedProp.boolValue = EditorGUILayout.Toggle(exposedProp.boolValue, GUILayout.Width(18));

                        DrawDefaultField(defaultJsonProp, newType);
                        DrawValueField(key, newType, valuesEditable);

                        using (new EditorGUI.DisabledScope(!valuesEditable || !DSM.Store.HasOverride(key)))
                        {
                            if (GUILayout.Button("Reset", GUILayout.Width(50)))
                            {
                                DSM.Reset(key);
                                if (!EditorApplication.isPlaying) DSM.Save();
                            }
                        }

                        if (GUILayout.Button("✕", GUILayout.Width(24)))
                            pendingRemoveIndex = TryRemoveEntry(key, i);
                    }
                }
            }

            if (pendingRemoveIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(pendingRemoveIndex);
        }

        private static void DrawDefaultField(SerializedProperty defaultJsonProp, DSMDataType type)
        {
            JToken? defaultToken;
            try { defaultToken = JToken.Parse(defaultJsonProp.stringValue); }
            catch { defaultToken = null; }

            var newDefaultToken = DSMValueField.DrawField(GUIContent.none, type, defaultToken, out var changed);
            if (changed)
                defaultJsonProp.stringValue = newDefaultToken.ToString(Formatting.None);
        }

        private static void DrawValueField(string key, DSMDataType type, bool valuesEditable)
        {
            var hasOverride = valuesEditable && DSM.Store.HasOverride(key);
            GUILayout.Label(hasOverride ? "●" : " ", hasOverride ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(12));

            using (new EditorGUI.DisabledScope(!valuesEditable))
            {
                var valueToken = valuesEditable ? DSM.Store.GetEffectiveToken(key) : null;
                var newValueToken = DSMValueField.DrawField(GUIContent.none, type, valueToken, out var changed);
                if (valuesEditable && changed)
                {
                    DSM.Store.SetToken(key, newValueToken);
                    if (!EditorApplication.isPlaying) DSM.Save();
                }
            }
        }

        // Returns the index to remove after the dialog(s), or -1 if the user cancelled.
        private static int TryRemoveEntry(string key, int index)
        {
            if (!EditorUtility.DisplayDialog("Remove Entry", $"Remove entry '{key}' from the config?", "Remove", "Cancel"))
                return -1;

            if (EditorUtility.DisplayDialog("Reset Value", $"Also reset the current value for '{key}'?", "Reset", "Keep"))
            {
                DSM.Reset(key);
                if (!EditorApplication.isPlaying) DSM.Save();
            }

            return index;
        }

        private void DrawAddEntry()
        {
            EditorGUILayout.LabelField("Add Entry", EditorStyles.boldLabel);

            var error = DSMEntryRules.ValidateNewKey(_config!, _newKey);

            using (new EditorGUILayout.HorizontalScope())
            {
                _newKey = EditorGUILayout.TextField(_newKey, GUILayout.Width(150));
                _newType = (DSMDataType)EditorGUILayout.EnumPopup(_newType, GUILayout.Width(90));

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        AddEntry(_newKey.Trim(), _newType);
                        _newKey = string.Empty;
                    }
                }
            }

            if (_newKey.Length > 0 && error != null)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void AddEntry(string key, DSMDataType type)
        {
            var entriesProp = _configSo!.FindProperty("_entries");
            var index = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(index);

            var element = entriesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_key").stringValue = key;
            element.FindPropertyRelative("_type").enumValueIndex = (int)type;
            element.FindPropertyRelative("_label").stringValue = string.Empty;
            element.FindPropertyRelative("_exposed").boolValue = false;
            element.FindPropertyRelative("_defaultJson").stringValue = DSMEntryDefinition.DefaultJsonFor(type);
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset All Values", GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog("Reset All Values", "Reset all overrides to their defaults?", "Reset", "Cancel"))
                    {
                        DSM.ResetAll();
                        if (!EditorApplication.isPlaying) DSM.Save();
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Overrides: {DSM.Store.OverrideKeys.Count}", EditorStyles.miniLabel);
            }
        }
    }
}
