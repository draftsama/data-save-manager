#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Edits a DSMConfig's entry definitions and, through <see cref="DSM"/>, the live store's values.</summary>
    internal sealed class DSMManagerWindow : EditorWindow
    {
        private static readonly Color DuplicateKeyColor = new(0.65f, 0.28f, 0.28f);

        private const float TypeWidth = 90f;
        private const float PrefixWidth = 58f;
        private const float ToggleWidth = 18f;
        private const float OverrideMarkWidth = 14f;
        private const float ResetWidth = 50f;
        private const float RemoveWidth = 24f;
        private const float CardPadding = 4f;
        private const float FieldSpacing = 4f;
        private const float InlineLabelWidth = 40f;

        private static readonly GUIContent KeyHeader = new("Key", "Identifier used in code: DSM.Get<T>(key).");
        private static readonly GUIContent TypeHeader = new("Type");
        private static readonly GUIContent LabelHeader = new("Label", "Display name in the Runtime Panel. Falls back to the key when empty.");
        private static readonly GUIContent ExposedHeader = new("Exposed", "Exposed: shown in the Runtime Panel.");
        private static readonly GUIContent DefaultHeader = new("Default", "Value used when there is no Override. Stored in the Config.");
        private static readonly GUIContent ValueHeader = new("Value", "Current value. \u25cf marks an Override stored in the Save File.");
        private static readonly GUIContent OverrideMark = new("\u25cf", "Overridden: this value is stored in the Save File. Reset returns it to the Default.");

        private DSMConfig? _config;
        private SerializedObject? _configSo;
        private ReorderableList? _entriesList;
        private string _search = string.Empty;
        private Vector2 _entriesScroll;
        private bool _settingsFoldout = true;
        private string _newKey = string.Empty;
        private DSMDataType _newType = DSMDataType.String;

        [MenuItem("Draft/DSM/Open Manager")]
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
            _entriesList = null; // Rebuilt lazily against the fresh SerializedObject in DrawEntries.
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
            var entriesProp = _configSo!.FindProperty("_entries");
            var filter = _search.Trim();
            var pendingRemoveIndex = -1;

            var list = GetOrCreateEntriesList(entriesProp);
            list.draggable = filter.Length == 0;

            bool Matches(int index)
            {
                if (filter.Length == 0) return true;
                var element = entriesProp.GetArrayElementAtIndex(index);
                var key = element.FindPropertyRelative("_key").stringValue;
                var label = element.FindPropertyRelative("_label").stringValue;
                return key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            list.elementHeightCallback = index => Matches(index) ? CardHeight : 0f;

            list.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                if (index < 0 || index >= entriesProp.arraySize || !Matches(index)) return;

                var type = (DSMDataType)entriesProp.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_type").enumValueIndex;
                DrawTypeTint(rect, type);
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (!Matches(index)) return;

                var element = entriesProp.GetArrayElementAtIndex(index);
                var keyProp = element.FindPropertyRelative("_key");
                var key = keyProp.stringValue;
                var isDuplicate = duplicates.Contains(key);

                if (DrawEntryCard(rect, element, isDuplicate, valuesEditable))
                    pendingRemoveIndex = index;
            };

            using (var scroll = new EditorGUILayout.ScrollViewScope(_entriesScroll, false, false, GUILayout.ExpandHeight(true)))
            {
                _entriesScroll = scroll.scrollPosition;
                list.DoLayoutList();
            }

            if (pendingRemoveIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(pendingRemoveIndex);
        }

        private ReorderableList GetOrCreateEntriesList(SerializedProperty entriesProp)
        {
            if (_entriesList != null && _entriesList.serializedProperty.serializedObject == _configSo)
            {
                _entriesList.serializedProperty = entriesProp;
                return _entriesList;
            }

            _entriesList = new ReorderableList(_configSo, entriesProp, true, true, false, false)
            {
                headerHeight = EditorGUIUtility.singleLineHeight + 4f,
                footerHeight = 0f
            };
            _entriesList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Entries — drag ≡ to reorder (this is the Runtime Panel order)");

            return _entriesList;
        }

        private const float TypeStripeWidth = 4f;
        private const float TypeTintAlpha = 0.12f;

        private static Color TypeColor(DSMDataType type) => type switch
        {
            DSMDataType.Int => new Color(0.30f, 0.60f, 1.00f),
            DSMDataType.Float => new Color(0.25f, 0.80f, 0.85f),
            DSMDataType.Bool => new Color(1.00f, 0.60f, 0.20f),
            DSMDataType.String => new Color(0.45f, 0.85f, 0.35f),
            DSMDataType.Vector2 => new Color(0.75f, 0.45f, 1.00f),
            DSMDataType.Vector3 => new Color(1.00f, 0.40f, 0.75f),
            DSMDataType.Color => new Color(1.00f, 0.85f, 0.25f),
            _ => Color.gray
        };

        private static void DrawTypeTint(Rect rect, DSMDataType type)
        {
            var color = TypeColor(type);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, TypeTintAlpha));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, TypeStripeWidth, rect.height), color);
        }

        private static float CardHeight =>
            CardPadding * 2f +
            EditorGUIUtility.singleLineHeight * 4f +
            EditorGUIUtility.standardVerticalSpacing * 3f;

        // Every line shares one prefix column (label + override-mark slot) so all fields start at the same x.
        private static float FieldStart(Rect line) => line.x + PrefixWidth + OverrideMarkWidth;

        // Returns true when the row's remove button was clicked; the caller defers the actual
        // DeleteArrayElementAtIndex until after the list has finished drawing.
        private static bool DrawEntryCard(Rect rect, SerializedProperty elementProp, bool isDuplicate, bool valuesEditable)
        {
            var keyProp = elementProp.FindPropertyRelative("_key");
            var typeProp = elementProp.FindPropertyRelative("_type");
            var labelProp = elementProp.FindPropertyRelative("_label");
            var exposedProp = elementProp.FindPropertyRelative("_exposed");
            var defaultJsonProp = elementProp.FindPropertyRelative("_defaultJson");
            var key = keyProp.stringValue;
            var type = (DSMDataType)typeProp.enumValueIndex;

            var lineH = EditorGUIUtility.singleLineHeight;
            var vSpace = EditorGUIUtility.standardVerticalSpacing;
            var line1 = new Rect(rect.x, rect.y + CardPadding, rect.width, lineH);
            var line2 = new Rect(rect.x, line1.yMax + vSpace, rect.width, lineH);
            var line3 = new Rect(rect.x, line2.yMax + vSpace, rect.width, lineH);
            var line4 = new Rect(rect.x, line3.yMax + vSpace, rect.width, lineH);

            var priorLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = InlineLabelWidth;

            var removeClicked = DrawKeyLine(line1, keyProp, typeProp, defaultJsonProp, isDuplicate, ref type);
            DrawExposedLine(line2, exposedProp, labelProp);
            DrawDefaultField(line3, defaultJsonProp, type);
            DrawValueLine(line4, key, type, valuesEditable);

            EditorGUIUtility.labelWidth = priorLabelWidth;
            return removeClicked && TryRemoveEntry(key);
        }

        private static bool DrawKeyLine(
            Rect rect,
            SerializedProperty keyProp,
            SerializedProperty typeProp,
            SerializedProperty defaultJsonProp,
            bool isDuplicate,
            ref DSMDataType type)
        {
            var removeRect = new Rect(rect.xMax - RemoveWidth, rect.y, RemoveWidth, rect.height);
            var typeRect = new Rect(removeRect.x - FieldSpacing - TypeWidth, rect.y, TypeWidth, rect.height);
            var fieldX = FieldStart(rect);
            var keyRect = new Rect(fieldX, rect.y, Mathf.Max(60f, typeRect.x - FieldSpacing - fieldX), rect.height);

            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), KeyHeader);
            var priorColor = GUI.backgroundColor;
            if (isDuplicate) GUI.backgroundColor = DuplicateKeyColor;
            keyProp.stringValue = EditorGUI.TextField(keyRect, keyProp.stringValue);
            GUI.backgroundColor = priorColor;

            var newType = (DSMDataType)EditorGUI.EnumPopup(typeRect, TypeHeader, type);
            if (newType != type)
            {
                typeProp.enumValueIndex = (int)newType;
                defaultJsonProp.stringValue = DSMEntryDefinition.DefaultJsonFor(newType);
                type = newType;
            }

            return GUI.Button(removeRect, "✕");
        }

        private static void DrawExposedLine(Rect rect, SerializedProperty exposedProp, SerializedProperty labelProp)
        {
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), ExposedHeader);
            var toggleRect = new Rect(fieldX, rect.y, ToggleWidth, rect.height);
            exposedProp.boolValue = EditorGUI.Toggle(toggleRect, exposedProp.boolValue);

            if (!exposedProp.boolValue) return;

            var labelX = toggleRect.xMax + FieldSpacing;
            var labelRect = new Rect(labelX, rect.y, rect.xMax - RemoveWidth - FieldSpacing - labelX, rect.height);
            labelProp.stringValue = EditorGUI.TextField(labelRect, LabelHeader, labelProp.stringValue);
        }

        private static void DrawDefaultField(Rect rect, SerializedProperty defaultJsonProp, DSMDataType type)
        {
            JToken? defaultToken;
            try { defaultToken = JToken.Parse(defaultJsonProp.stringValue); }
            catch { defaultToken = null; }

            var fieldX = FieldStart(rect);
            var fieldRect = new Rect(fieldX, rect.y, rect.xMax - ResetWidth - FieldSpacing - fieldX, rect.height);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), DefaultHeader);

            var newDefaultToken = DSMValueField.DrawField(fieldRect, GUIContent.none, type, defaultToken, out var changed);
            if (changed)
                defaultJsonProp.stringValue = newDefaultToken.ToString(Formatting.None);
        }

        private static void DrawValueLine(Rect rect, string key, DSMDataType type, bool valuesEditable)
        {
            var hasOverride = valuesEditable && DSM.Store.HasOverride(key);

            var resetRect = new Rect(rect.xMax - ResetWidth, rect.y, ResetWidth, rect.height);
            var labelRect = new Rect(rect.x, rect.y, PrefixWidth, rect.height);
            var markRect = new Rect(labelRect.xMax, rect.y, OverrideMarkWidth, rect.height);
            var fieldX = FieldStart(rect);
            var fieldRect = new Rect(fieldX, rect.y, resetRect.x - FieldSpacing - fieldX, rect.height);

            EditorGUI.LabelField(labelRect, ValueHeader);
            GUI.Label(markRect, hasOverride ? OverrideMark : GUIContent.none, hasOverride ? EditorStyles.boldLabel : EditorStyles.label);

            using (new EditorGUI.DisabledScope(!valuesEditable))
            {
                var valueToken = valuesEditable ? DSM.Store.GetEffectiveToken(key) : null;
                var newValueToken = DSMValueField.DrawField(fieldRect, GUIContent.none, type, valueToken, out var changed);
                if (valuesEditable && changed)
                {
                    DSM.Store.SetToken(key, newValueToken);
                    if (!EditorApplication.isPlaying) DSM.Save();
                }
            }

            using (new EditorGUI.DisabledScope(!valuesEditable || !DSM.Store.HasOverride(key)))
            {
                if (GUI.Button(resetRect, "Reset"))
                {
                    DSM.Reset(key);
                    if (!EditorApplication.isPlaying) DSM.Save();
                }
            }
        }

        // Returns true when the user confirmed removal; the caller still owns deleting the
        // array element, deferred until after the list has finished drawing.
        private static bool TryRemoveEntry(string key)
        {
            if (!EditorUtility.DisplayDialog("Remove Entry", $"Remove entry '{key}' from the config?", "Remove", "Cancel"))
                return false;

            if (EditorUtility.DisplayDialog("Reset Value", $"Also reset the current value for '{key}'?", "Reset", "Keep"))
            {
                DSM.Reset(key);
                if (!EditorApplication.isPlaying) DSM.Save();
            }

            return true;
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
