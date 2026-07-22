#nullable enable
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class DSMManagerWindow : EditorWindow
{
    // ── State ────────────────────────────────────────────────────────────────

    private readonly DSMManagerSlotOps _slotOps = new();
    private DSMConfig? _config;
    private UnityEditor.SerializedObject? _configSO;
    private string _searchText = string.Empty;
    private Vector2 _listScroll;
    private bool _configExpanded = true;
    private bool _showAddPanel;
    private bool _showNewSlotInput;
    private string _newSlotName = string.Empty;

    // Add-panel transient fields
    private string _newKey = string.Empty;
    private DSMDataType _newType = DSMDataType.String;
    private string _newSerializedDefault = string.Empty;
    private bool _newBool;
    private float _newFloat;
    private int _newInt;
    private long _newLong;
    private double _newDouble;
    private Vector2 _newVec2;
    private Vector3 _newVec3;
    private Vector4 _newVec4;
    private Color _newColor = Color.white;

    // ── Styles ───────────────────────────────────────────────────────────────

    private static GUIStyle? s_headerLabel;
    private static GUIStyle? s_sectionBox;
    private static GUIStyle? s_rowBox;
    private static GUIStyle? s_deleteBtn;
    private static GUIStyle? s_keyLabel;

    private void EnsureStyles()
    {
        s_headerLabel ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
        s_sectionBox ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(4, 4, 2, 2) };
        s_rowBox ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(0, 0, 1, 1) };
        s_deleteBtn ??= new GUIStyle(EditorStyles.miniButton) { normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }, fontStyle = FontStyle.Bold };
        s_keyLabel ??= new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [MenuItem("DSM/Open Manager")]
    public static void Open()
    {
        var win = GetWindow<DSMManagerWindow>("DSM Manager");
        win.minSize = new Vector2(600, 520);
        win.Show();
    }

    private void OnEnable() => Reload();

    public void Reload()
    {
        _config = Resources.Load<DSMConfig>("DSMConfig");
        _configSO = _config != null ? new UnityEditor.SerializedObject(_config) : null;
        _slotOps.BindConfig(_config, _configSO);
        _slotOps.ResetDefaults(DSMConstantReflectionCache.GetDefaults());
        _slotOps.DiscoverSlots();
        if (!_slotOps.AvailableSlots.Any(s => s == _slotOps.ActiveSlot))
            _slotOps.ActiveSlot = _slotOps.GetSlotName();
        _slotOps.DefaultsDirty = false;
        _slotOps.LoadSlotData(_slotOps.ActiveSlot);
        Repaint();
    }

    // ── Main GUI ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();
        DrawError();
        DrawConfigSection();
        DrawSlotBar();
        DrawNewSlotInput();
        DrawSearchBar();
        DrawEntryList();
        DrawAddPanel();
        DrawFooter();
    }

    private void DrawError()
    {
        if (string.IsNullOrEmpty(_slotOps.LastError)) return;
        EditorGUILayout.HelpBox(_slotOps.LastError + " See the Console for details.", MessageType.Error);
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        using var h = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
        GUILayout.Label("DSM Manager", s_headerLabel!, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            Reload();
    }

    // ── Config section ────────────────────────────────────────────────────────

    private void DrawConfigSection()
    {
        _configExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_configExpanded, "Configuration");
        if (_configExpanded)
        {
            using var box = new EditorGUILayout.VerticalScope(s_sectionBox!);
            var picked = (DSMConfig?)EditorGUILayout.ObjectField("DSM Config", _config, typeof(DSMConfig), false);
            if (picked != _config) { _config = picked; Reload(); }

            if (_config == null)
            {
                EditorGUILayout.HelpBox("No DSMConfig found. Create one via DSM > Create Config Asset.", MessageType.Warning);
                if (GUILayout.Button("Create Config Asset", GUILayout.Height(26))) CreateConfigAsset();
            }
            else
            {
                _configSO!.Update();
                EditorGUILayout.Space(4);
                DrawConfigToggles();
                EditorGUILayout.Space(3);
                DrawConfigSlotRow();
                _configSO.ApplyModifiedProperties();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        DrawSeparator();
    }

    private void DrawConfigToggles()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Auto Save", EditorStyles.miniLabel, GUILayout.Width(62));
            ConfigProp("_autoSave").boolValue =
                EditorGUILayout.Toggle(ConfigProp("_autoSave").boolValue, GUILayout.Width(16));
            GUILayout.Space(14);
            GUILayout.Label("Debounce", EditorStyles.miniLabel, GUILayout.Width(58));
            ConfigProp("_autoSaveDebounce").floatValue =
                EditorGUILayout.FloatField(ConfigProp("_autoSaveDebounce").floatValue, GUILayout.Width(38));
            GUILayout.Label("s", EditorStyles.miniLabel, GUILayout.Width(10));
            GUILayout.Space(14);
            GUILayout.Label("Encrypt", EditorStyles.miniLabel, GUILayout.Width(48));
            ConfigProp("_encrypt").boolValue =
                EditorGUILayout.Toggle(ConfigProp("_encrypt").boolValue, GUILayout.Width(16));
            GUILayout.Space(14);
            GUILayout.Label("Pretty", EditorStyles.miniLabel, GUILayout.Width(38));
            ConfigProp("_prettyPrint").boolValue =
                EditorGUILayout.Toggle(ConfigProp("_prettyPrint").boolValue, GUILayout.Width(16));
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawConfigSlotRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Default Slot", EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label(_config!.DefaultSlot, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Edit Config →", GUILayout.Width(100)))
                Selection.activeObject = _config;
        }
    }

    // ── Slot bar ──────────────────────────────────────────────────────────────

    private void DrawSlotBar()
    {
        using var h = new EditorGUILayout.HorizontalScope();
        GUILayout.Label("Slot", EditorStyles.miniLabel, GUILayout.Width(28));

        var idx = Array.IndexOf(_slotOps.AvailableSlots, _slotOps.ActiveSlot);
        if (idx < 0) idx = 0;
        var newIdx = EditorGUILayout.Popup(idx, _slotOps.AvailableSlots, GUILayout.Width(120));
        if (newIdx != idx)
            _slotOps.SelectSlot(_slotOps.AvailableSlots[newIdx]);

        var isDefault = _config != null && _slotOps.ActiveSlot == _config.DefaultSlot;
        using (new EditorGUI.DisabledScope(isDefault))
        {
            if (GUILayout.Button(isDefault ? "✓ Default" : "Set Default", EditorStyles.miniButton, GUILayout.Width(76)))
            {
                _slotOps.SetDefaultSlot(_slotOps.ActiveSlot);
                Repaint();
            }
        }

        GUILayout.Space(4);
        var addLabel = _showNewSlotInput ? "Cancel" : "+ New";
        if (GUILayout.Button(addLabel, EditorStyles.miniButton, GUILayout.Width(50)))
        {
            _showNewSlotInput = !_showNewSlotInput;
            _newSlotName = string.Empty;
        }

        using (new EditorGUI.DisabledScope(_slotOps.AvailableSlots.Length <= 1))
        {
            if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(46)))
                DeleteActiveSlot();
        }
        GUILayout.FlexibleSpace();
    }

    private void DrawNewSlotInput()
    {
        if (!_showNewSlotInput) return;
        using var h = new EditorGUILayout.HorizontalScope();
        GUILayout.Label("Name", EditorStyles.miniLabel, GUILayout.Width(38));
        _newSlotName = EditorGUILayout.TextField(_newSlotName);
        var trimmed = _newSlotName.Trim();
        var valid = !string.IsNullOrWhiteSpace(trimmed) &&
                    !_slotOps.AvailableSlots.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        using (new EditorGUI.DisabledScope(!valid))
        {
            if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                _slotOps.CreateSlot(trimmed);
                _showNewSlotInput = false;
            }
        }
    }

    private void DeleteActiveSlot()
    {
        if (!EditorUtility.DisplayDialog("Delete Slot",
            $"Delete slot '{_slotOps.ActiveSlot}'? This cannot be undone.", "Delete", "Cancel")) return;
        _slotOps.DeleteActiveSlot();
    }

    // ── Search bar ────────────────────────────────────────────────────────────

    private void DrawSearchBar()
    {
        DrawSeparator();
        using var h = new EditorGUILayout.HorizontalScope();
        GUILayout.Label("🔍", GUILayout.Width(18));
        _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
        var label = _showAddPanel ? "▲ Cancel" : "+ New Entry";
        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(88)))
        {
            _showAddPanel = !_showAddPanel;
            if (_showAddPanel) ResetAddPanel();
        }
    }

    // ── Entry list ────────────────────────────────────────────────────────────

    private void DrawEntryList()
    {
        // Union: defaults keys first, then any extra keys only in slot
        var allKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in _slotOps.Defaults)      { if (seen.Add(d.Key)) allKeys.Add(d.Key); }
        foreach (var k in _slotOps.SlotData.Keys) { if (seen.Add(k))     allKeys.Add(k); }

        if (allKeys.Count == 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "No data. Add entries with '+ New Entry' or run the game with DSM.Set().",
                MessageType.Info);
            return;
        }

        var filter = _searchText.Trim().ToLower(CultureInfo.InvariantCulture);
        string? toRemoveKey = null;

        using var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.ExpandHeight(true));
        _listScroll = scroll.scrollPosition;

        foreach (var key in allKeys)
        {
            if (!string.IsNullOrEmpty(filter) && !key.ToLower(CultureInfo.InvariantCulture).Contains(filter))
                continue;

            var defEntry = _slotOps.Defaults.Find(e => e.Key == key);
            _slotOps.SlotData.TryGetValue(key, out var currentToken);
            var displayType = defEntry?.Type ?? (currentToken != null ? DSMManagerSlotOps.InferFromToken(currentToken).Item1 : DSMDataType.String);
            var currentStr  = currentToken != null ? DSMManagerSlotOps.InferFromToken(currentToken).Item2 : (defEntry?.SerializedDefault ?? string.Empty);

            using var row = new EditorGUILayout.VerticalScope(s_rowBox!);
            EditorGUI.DrawRect(row.rect, TypeColor(displayType));

            // ── Header row: key + type + expose + delete ─────────────────────
            var isExposed = _config?.FindExposed(key) != null;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("key:", GUILayout.Width(36));
                GUILayout.TextField(key, GUILayout.Width(120));
                                GUILayout.EndHorizontal();


                if (defEntry != null)
                {
                    var newType = (DSMDataType)EditorGUILayout.EnumPopup(defEntry.Type, GUILayout.Width(80));
                    if (newType != defEntry.Type) { defEntry.Type = newType; _slotOps.DefaultsDirty = true; }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Expose", EditorStyles.miniLabel, GUILayout.Width(42));
                    var newExposed = EditorGUILayout.Toggle(isExposed, GUILayout.Width(16));
                    if (newExposed != isExposed)
                    {
                        if (newExposed) _config?.SetExposed(key, string.Empty, defEntry.Type);
                        else _config?.RemoveExposed(key);
                        SaveConfig();
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.EnumPopup(displayType, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                }

                if (GUILayout.Button("✕", s_deleteBtn!, GUILayout.Width(26)))
                    toRemoveKey = key;
            }

            // ── Expose description row (shown when exposed) ───────────────────
            if (isExposed && defEntry != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Description", EditorStyles.miniLabel, GUILayout.Width(68));
                    var currentLabel = _config?.FindExposed(key)?.Label ?? string.Empty;
                    var newLabel = EditorGUILayout.TextField(currentLabel, GUILayout.ExpandWidth(true));
                    if (newLabel != currentLabel)
                    {
                        _config?.SetExposed(key, newLabel, defEntry.Type);
                        SaveConfig();
                    }
                }
            }

            // ── Default value row ─────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Default", EditorStyles.miniLabel, GUILayout.Width(52));
                if (defEntry != null)
                {
                    EditorGUI.BeginChangeCheck();
                    var newDef = DrawValueField(defEntry.Type, defEntry.SerializedDefault, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck() && newDef != defEntry.SerializedDefault)
                    {
                        defEntry.SerializedDefault = newDef;
                        _slotOps.DefaultsDirty = true;
                    }
                }
                else
                {
                    GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true));
                }
            }

            // ── Current value row ─────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Current", EditorStyles.miniLabel, GUILayout.Width(52));
                EditorGUI.BeginChangeCheck();
                var newCurrent = DrawValueField(displayType, currentStr, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && newCurrent != currentStr)
                {
                    var jObj = _slotOps.ReadSlotJObject(_slotOps.ActiveSlot) ?? new JObject();
                    jObj[key] = DSMManagerSlotOps.EntryToJToken(new DSMDataEntry { Key = key, Type = displayType, SerializedDefault = newCurrent });
                    _slotOps.WriteSlotJObject(_slotOps.ActiveSlot, jObj);
                    _slotOps.SlotData[key] = jObj[key]!;
                }
            }
        }

        if (toRemoveKey != null)
        {
            if (EditorUtility.DisplayDialog("Remove Entry",
                $"Remove '{toRemoveKey}' from DSMConstant and '{_slotOps.ActiveSlot}' slot?", "Remove", "Cancel"))
            {
                _slotOps.Defaults.RemoveAll(e => e.Key == toRemoveKey);
                _slotOps.DefaultsDirty = true;
                _slotOps.PropagateToAllSlots(jObj => jObj.Remove(toRemoveKey));
            }
        }
    }

    private static string DrawValueField(DSMDataType type, string current, params GUILayoutOption[] opts)
    {
        return type switch
        {
            DSMDataType.Bool   => EditorGUILayout.Toggle(bool.TryParse(current, out var bv) && bv, opts).ToString(),
            DSMDataType.Int    => EditorGUILayout.IntField(int.TryParse(current, out var iv) ? iv : 0, opts).ToString(),
            DSMDataType.Float  => EditorGUILayout.FloatField(
                float.TryParse(current, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv) ? fv : 0f, opts)
                .ToString("G", CultureInfo.InvariantCulture),
            DSMDataType.Double => EditorGUILayout.DoubleField(
                double.TryParse(current, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) ? dv : 0.0, opts)
                .ToString("G", CultureInfo.InvariantCulture),
            DSMDataType.Long   => EditorGUILayout.LongField(long.TryParse(current, out var lv) ? lv : 0L, opts).ToString(),
            DSMDataType.String => EditorGUILayout.TextField(current, opts),
            DSMDataType.Vector2 => Vec2ToStr(EditorGUILayout.Vector2Field(GUIContent.none, StrToVec2(current), opts)),
            DSMDataType.Vector3 => Vec3ToStr(EditorGUILayout.Vector3Field(GUIContent.none, StrToVec3(current), opts)),
            DSMDataType.Vector4 => Vec4ToStr(EditorGUILayout.Vector4Field(GUIContent.none, StrToVec4(current), opts)),
            DSMDataType.Color   => ColorToStr(EditorGUILayout.ColorField(StrToColor(current), opts)),
            _ => EditorGUILayout.TextField(current, opts)
        };
    }

    // ── Add panel ─────────────────────────────────────────────────────────────

    private void DrawAddPanel()
    {
        if (!_showAddPanel) return;
        DrawSeparator();
        using var box = new EditorGUILayout.VerticalScope(s_sectionBox!);
        GUILayout.Label("New Entry", EditorStyles.boldLabel);

        _newKey = EditorGUILayout.TextField("Key", _newKey);
        var prevType = _newType;
        _newType = (DSMDataType)EditorGUILayout.EnumPopup("Type", _newType);
        if (_newType != prevType) ResetAddPanelValues();
        GUILayout.Label("Default Value", EditorStyles.label);
        DrawAddDefaultField();

        var keyExists = _slotOps.Defaults.Exists(e => e.Key == _newKey.Trim());
        if (!string.IsNullOrWhiteSpace(_newKey) && keyExists)
            EditorGUILayout.HelpBox($"Key '{_newKey.Trim()}' already exists in DSMConstant.", MessageType.Warning);

        EditorGUILayout.Space(4);
        using var btnRow = new EditorGUILayout.HorizontalScope();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Cancel", GUILayout.Width(70))) _showAddPanel = false;
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newKey) || keyExists))
        {
            if (GUILayout.Button("✚ Create", GUILayout.Width(80)))
            {
                _slotOps.CommitNewEntry(_newKey.Trim(), _newType, _newSerializedDefault);
                _showAddPanel = false;
            }
        }
    }

    private void DrawAddDefaultField()
    {
        switch (_newType)
        {
            case DSMDataType.Bool:   _newBool   = EditorGUILayout.Toggle(_newBool);                    _newSerializedDefault = _newBool.ToString();                                          break;
            case DSMDataType.Int:    _newInt    = EditorGUILayout.IntField(_newInt);                   _newSerializedDefault = _newInt.ToString();                                            break;
            case DSMDataType.Float:  _newFloat  = EditorGUILayout.FloatField(_newFloat);              _newSerializedDefault = _newFloat.ToString("G", CultureInfo.InvariantCulture);        break;
            case DSMDataType.Double: _newDouble = EditorGUILayout.DoubleField(_newDouble);            _newSerializedDefault = _newDouble.ToString("G", CultureInfo.InvariantCulture);       break;
            case DSMDataType.Long:   _newLong   = EditorGUILayout.LongField(_newLong);                _newSerializedDefault = _newLong.ToString();                                           break;
            case DSMDataType.String: _newSerializedDefault = EditorGUILayout.TextField(_newSerializedDefault);                                                                               break;
            case DSMDataType.Vector2: _newVec2  = EditorGUILayout.Vector2Field(GUIContent.none, _newVec2); _newSerializedDefault = Vec2ToStr(_newVec2);                                     break;
            case DSMDataType.Vector3: _newVec3  = EditorGUILayout.Vector3Field(GUIContent.none, _newVec3); _newSerializedDefault = Vec3ToStr(_newVec3);                                     break;
            case DSMDataType.Vector4: _newVec4  = EditorGUILayout.Vector4Field(GUIContent.none, _newVec4); _newSerializedDefault = Vec4ToStr(_newVec4);                                     break;
            case DSMDataType.Color:   _newColor = EditorGUILayout.ColorField(_newColor);              _newSerializedDefault = ColorToStr(_newColor);                                         break;
        }
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private void DrawFooter()
    {
        DrawSeparator();
        using var h = new EditorGUILayout.HorizontalScope();
        var statusLabel = _slotOps.DefaultsDirty
            ? $"{_slotOps.Defaults.Count} defaults  ·  {_slotOps.SlotData.Count} in [{_slotOps.ActiveSlot}]  ·  ● unsaved changes"
            : $"{_slotOps.Defaults.Count} defaults  ·  {_slotOps.SlotData.Count} in [{_slotOps.ActiveSlot}]";
        GUILayout.Label(statusLabel, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(_slotOps.Defaults.Count == 0 || !_slotOps.DefaultsDirty))
        {
            if (GUILayout.Button("Save DSMConstant.cs", GUILayout.Height(26), GUILayout.Width(170)))
            {
                DSMCodeGenerator.Generate(_slotOps.Defaults);
                _slotOps.DefaultsDirty = false;
            }
        }
    }

    // ── Config helpers ────────────────────────────────────────────────────────

    private UnityEditor.SerializedProperty ConfigProp(string backingField) =>
        _configSO!.FindProperty(backingField);

    private void SaveConfig()
    {
        if (_config == null) return;
        EditorUtility.SetDirty(_config);
        AssetDatabase.SaveAssets();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void DrawSeparator()
    {
        var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        EditorGUILayout.Space(2);
    }

    private void ResetAddPanel() { _newKey = string.Empty; _newType = DSMDataType.String; ResetAddPanelValues(); }
    private void ResetAddPanelValues()
    {
        _newBool = false; _newInt = 0; _newFloat = 0f; _newDouble = 0.0; _newLong = 0L;
        _newVec2 = Vector2.zero; _newVec3 = Vector3.zero; _newVec4 = Vector4.zero; _newColor = Color.white;
        _newSerializedDefault = DSMConstantReflectionCache.GetTypeDefault(_newType);
    }

    private static void CreateConfigAsset()
    {
        Directory.CreateDirectory("Assets/Resources");
        var asset = CreateInstance<DSMConfig>();
        AssetDatabase.CreateAsset(asset, "Assets/Resources/DSMConfig.asset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
    }

    // ── Type colors ──────────────────────────────────────────────────────────

    private static Color TypeColor(DSMDataType type) => type switch
    {
        DSMDataType.Int     => new Color(0.15f, 0.28f, 0.50f, 0.30f),
        DSMDataType.Float   => new Color(0.15f, 0.38f, 0.38f, 0.30f),
        DSMDataType.Double  => new Color(0.10f, 0.32f, 0.45f, 0.30f),
        DSMDataType.Long    => new Color(0.22f, 0.18f, 0.48f, 0.30f),
        DSMDataType.Bool    => new Color(0.48f, 0.30f, 0.08f, 0.30f),
        DSMDataType.String  => new Color(0.42f, 0.38f, 0.08f, 0.30f),
        DSMDataType.Vector2 => new Color(0.48f, 0.15f, 0.15f, 0.30f),
        DSMDataType.Vector3 => new Color(0.50f, 0.12f, 0.22f, 0.30f),
        DSMDataType.Vector4 => new Color(0.42f, 0.10f, 0.38f, 0.30f),
        DSMDataType.Color   => new Color(0.35f, 0.22f, 0.40f, 0.30f),
        _                   => Color.clear
    };

    // ── Vector / Color helpers ────────────────────────────────────────────────

    private static Vector2 StrToVec2(string s) { var p = s.Split(','); return new Vector2(Pf(p,0), Pf(p,1)); }
    private static Vector3 StrToVec3(string s) { var p = s.Split(','); return new Vector3(Pf(p,0), Pf(p,1), Pf(p,2)); }
    private static Vector4 StrToVec4(string s) { var p = s.Split(','); return new Vector4(Pf(p,0), Pf(p,1), Pf(p,2), Pf(p,3)); }
    private static Color StrToColor(string s) { var p = s.Split(','); return new Color(Pf(p,0), Pf(p,1), Pf(p,2), p.Length >= 4 ? Pf(p,3) : 1f); }
    private static float Pf(string[] parts, int i) =>
        i < parts.Length && float.TryParse(parts[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    private static string Fs(float v) => v.ToString("G", CultureInfo.InvariantCulture);
    private static string Vec2ToStr(Vector2 v) => $"{Fs(v.x)},{Fs(v.y)}";
    private static string Vec3ToStr(Vector3 v) => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)}";
    private static string Vec4ToStr(Vector4 v) => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)},{Fs(v.w)}";
    private static string ColorToStr(Color c) => $"{Fs(c.r)},{Fs(c.g)},{Fs(c.b)},{Fs(c.a)}";
}

#endif
