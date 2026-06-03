#nullable enable
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class DSMManagerWindow : EditorWindow
{
    // ── State ────────────────────────────────────────────────────────────────

    private List<DSMDataEntry> _defaults = new();          // read from DSMConstant via reflection
    private Dictionary<string, JToken> _slotData = new(); // current values from active slot
    private string _activeSlot = "default";
    private string[] _availableSlots = Array.Empty<string>();
    private DSMConfig? _config;
    private UnityEditor.SerializedObject? _configSO;
    private string _searchText = string.Empty;
    private Vector2 _listScroll;
    private bool _configExpanded = true;
    private bool _showAddPanel;
    private bool _showNewSlotInput;
    private string _newSlotName = string.Empty;
    private bool _defaultsDirty;

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
        LoadDefaultsFromReflection();
        DiscoverSlots();
        if (!_availableSlots.Any(s => s == _activeSlot))
            _activeSlot = GetSlotName();
        _defaultsDirty = false;
        LoadSlotData(_activeSlot);
        Repaint();
    }

    // ── Reflection: defaults from DSMConstant ─────────────────────────────────

    private void LoadDefaultsFromReflection()
    {
        _defaults = new List<DSMDataEntry>();

        Type? constantType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
                    { constantType = t; break; }
                }
            }
            catch (ReflectionTypeLoadException) { /* Expected — some assemblies cannot be fully reflected */ }
            catch (Exception ex) { Debug.LogWarning($"DSM: Unexpected exception scanning assembly for DSMConstant: {ex.Message}"); }
            if (constantType != null) break;
        }

        if (constantType == null) return;

        foreach (var field in constantType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null);
            if (value == null) continue;
            _defaults.Add(new DSMDataEntry
            {
                Key = field.Name,
                Type = FieldToType(field.FieldType),
                SerializedDefault = ValueToSerialized(value)
            });
        }
    }

    private static DSMDataType FieldToType(Type t)
    {
        if (t == typeof(int))     return DSMDataType.Int;
        if (t == typeof(float))   return DSMDataType.Float;
        if (t == typeof(double))  return DSMDataType.Double;
        if (t == typeof(long))    return DSMDataType.Long;
        if (t == typeof(bool))    return DSMDataType.Bool;
        if (t == typeof(string))  return DSMDataType.String;
        if (t == typeof(Vector2)) return DSMDataType.Vector2;
        if (t == typeof(Vector3)) return DSMDataType.Vector3;
        if (t == typeof(Vector4)) return DSMDataType.Vector4;
        if (t == typeof(Color))   return DSMDataType.Color;
        return DSMDataType.String;
    }

    private static string ValueToSerialized(object value) => value switch
    {
        Vector2 v => $"{Fs(v.x)},{Fs(v.y)}",
        Vector3 v => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)}",
        Vector4 v => $"{Fs(v.x)},{Fs(v.y)},{Fs(v.z)},{Fs(v.w)}",
        Color c   => $"{Fs(c.r)},{Fs(c.g)},{Fs(c.b)},{Fs(c.a)}",
        float f   => f.ToString("G", CultureInfo.InvariantCulture),
        double d  => d.ToString("G", CultureInfo.InvariantCulture),
        _         => value.ToString() ?? string.Empty
    };

    // ── Slot I/O ──────────────────────────────────────────────────────────────

    private string GetSlotName() =>
        string.IsNullOrEmpty(_config?.DefaultSlot) ? "default" : _config.DefaultSlot;

    private void DiscoverSlots()
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        names.Add(GetSlotName());
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(f);
                if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".enc",  StringComparison.OrdinalIgnoreCase))
                    names.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        _availableSlots = names.OrderBy(n => n).ToArray();
    }

    private void LoadSlotData(string slot)
    {
        _slotData = new Dictionary<string, JToken>(StringComparer.Ordinal);
        var jObj = ReadSlotJObject(slot);
        if (jObj == null) return;
        foreach (var prop in jObj.Properties())
            _slotData[prop.Name] = prop.Value;
        SyncRuntimeKeys();
    }

    private void SyncRuntimeKeys()
    {
        foreach (var kvp in _slotData)
        {
            if (_defaults.Exists(e => e.Key == kvp.Key)) continue;
            var (type, serialized) = InferFromToken(kvp.Value);
            _defaults.Add(new DSMDataEntry { Key = kvp.Key, Type = type, SerializedDefault = serialized });
            _defaultsDirty = true;
        }
    }

    private JObject? ReadSlotJObject(string slot)
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        var enc  = Path.Combine(dir, $"{slot}.enc");
        var json = Path.Combine(dir, $"{slot}.json");
        try
        {
            string content;
            if (File.Exists(enc))
                content = DSMEncryptor.Decrypt(File.ReadAllBytes(enc), _config?.EncryptionKey ?? string.Empty);
            else if (File.Exists(json))
                content = File.ReadAllText(json, Encoding.UTF8);
            else return null;
            return JObject.Parse(content);
        }
        catch { return null; }
    }

    private void WriteSlotJObject(string slot, JObject data)
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        Directory.CreateDirectory(dir);
        var pretty = _config?.PrettyPrint == true ? Formatting.Indented : Formatting.None;
        var json = data.ToString(pretty);
        if (_config?.Encrypt == true)
            File.WriteAllBytes(Path.Combine(dir, $"{slot}.enc"),
                DSMEncryptor.Encrypt(json, _config.EncryptionKey));
        else
            File.WriteAllText(Path.Combine(dir, $"{slot}.json"), json, Encoding.UTF8);
    }

    // ── Type inference from JToken ────────────────────────────────────────────

    private static (DSMDataType, string) InferFromToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Boolean: return (DSMDataType.Bool, token.Value<bool>().ToString());
            case JTokenType.Integer:
                var lv = token.Value<long>();
                return lv >= int.MinValue && lv <= int.MaxValue
                    ? (DSMDataType.Int, ((int)lv).ToString())
                    : (DSMDataType.Long, lv.ToString());
            case JTokenType.Float:
                return (DSMDataType.Float, token.Value<float>().ToString("G", CultureInfo.InvariantCulture));
            case JTokenType.String:
                return (DSMDataType.String, token.Value<string>() ?? string.Empty);
            case JTokenType.Object:
                return InferObjectToken((JObject)token);
            default:
                return (DSMDataType.String, token.ToString(Formatting.None));
        }
    }

    private static (DSMDataType, string) InferObjectToken(JObject obj)
    {
        var keys = obj.Properties().Select(p => p.Name).ToHashSet();
        if (keys.Contains("r") && keys.Contains("g") && keys.Contains("b"))
        {
            var r = obj["r"]?.Value<float>() ?? 1f; var g = obj["g"]?.Value<float>() ?? 1f;
            var b = obj["b"]?.Value<float>() ?? 1f; var a = obj["a"]?.Value<float>() ?? 1f;
            return (DSMDataType.Color, $"{Fs(r)},{Fs(g)},{Fs(b)},{Fs(a)}");
        }
        if (keys.Contains("x") && keys.Contains("y"))
        {
            var x = obj["x"]?.Value<float>() ?? 0f; var y = obj["y"]?.Value<float>() ?? 0f;
            if (keys.Contains("z"))
            {
                var z = obj["z"]?.Value<float>() ?? 0f;
                if (keys.Contains("w")) { var w = obj["w"]?.Value<float>() ?? 0f; return (DSMDataType.Vector4, $"{Fs(x)},{Fs(y)},{Fs(z)},{Fs(w)}"); }
                return (DSMDataType.Vector3, $"{Fs(x)},{Fs(y)},{Fs(z)}");
            }
            return (DSMDataType.Vector2, $"{Fs(x)},{Fs(y)}");
        }
        return (DSMDataType.String, obj.ToString(Formatting.None));
    }

    private static JToken EntryToJToken(DSMDataEntry e)
    {
        var d = e.SerializedDefault;
        return e.Type switch
        {
            DSMDataType.Int    => JToken.FromObject(int.TryParse(d, out var i) ? i : 0),
            DSMDataType.Float  => JToken.FromObject(float.TryParse(d, NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : 0f),
            DSMDataType.Double => JToken.FromObject(double.TryParse(d, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) ? dv : 0.0),
            DSMDataType.Long   => JToken.FromObject(long.TryParse(d, out var l) ? l : 0L),
            DSMDataType.Bool   => JToken.FromObject(bool.TryParse(d, out var b) && b),
            DSMDataType.String => JToken.FromObject(d),
            DSMDataType.Vector2 => Vec2Token(d),
            DSMDataType.Vector3 => Vec3Token(d),
            DSMDataType.Vector4 => Vec4Token(d),
            DSMDataType.Color   => ColorToken(d),
            _ => JToken.FromObject(d)
        };
    }

    private static JObject Vec2Token(string s) { var p = s.Split(','); return new JObject { ["x"] = Pf(p,0), ["y"] = Pf(p,1) }; }
    private static JObject Vec3Token(string s) { var p = s.Split(','); return new JObject { ["x"] = Pf(p,0), ["y"] = Pf(p,1), ["z"] = Pf(p,2) }; }
    private static JObject Vec4Token(string s) { var p = s.Split(','); return new JObject { ["x"] = Pf(p,0), ["y"] = Pf(p,1), ["z"] = Pf(p,2), ["w"] = Pf(p,3) }; }
    private static JObject ColorToken(string s) { var p = s.Split(','); return new JObject { ["r"] = Pf(p,0), ["g"] = Pf(p,1), ["b"] = Pf(p,2), ["a"] = p.Length >= 4 ? Pf(p,3) : 1f }; }

    // ── Main GUI ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();
        DrawConfigSection();
        DrawSlotBar();
        DrawNewSlotInput();
        DrawSearchBar();
        DrawEntryList();
        DrawAddPanel();
        DrawFooter();
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

        var idx = Array.IndexOf(_availableSlots, _activeSlot);
        if (idx < 0) idx = 0;
        var newIdx = EditorGUILayout.Popup(idx, _availableSlots, GUILayout.Width(120));
        if (newIdx != idx)
        {
            _activeSlot = _availableSlots[newIdx];
            LoadSlotData(_activeSlot);
        }

        var isDefault = _config != null && _activeSlot == _config.DefaultSlot;
        using (new EditorGUI.DisabledScope(isDefault))
        {
            if (GUILayout.Button(isDefault ? "✓ Default" : "Set Default", EditorStyles.miniButton, GUILayout.Width(76)))
                SetDefaultSlot(_activeSlot);
        }

        GUILayout.Space(4);
        var addLabel = _showNewSlotInput ? "Cancel" : "+ New";
        if (GUILayout.Button(addLabel, EditorStyles.miniButton, GUILayout.Width(50)))
        {
            _showNewSlotInput = !_showNewSlotInput;
            _newSlotName = string.Empty;
        }

        using (new EditorGUI.DisabledScope(_availableSlots.Length <= 1))
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
                    !_availableSlots.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        using (new EditorGUI.DisabledScope(!valid))
        {
            if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                var jObj = new JObject();
                foreach (var entry in _defaults)
                    jObj[entry.Key] = EntryToJToken(entry);
                WriteSlotJObject(trimmed, jObj);
                DiscoverSlots();
                _activeSlot = trimmed;
                LoadSlotData(_activeSlot);
                _showNewSlotInput = false;
            }
        }
    }

    private void DeleteActiveSlot()
    {
        if (!EditorUtility.DisplayDialog("Delete Slot",
            $"Delete slot '{_activeSlot}'? This cannot be undone.", "Delete", "Cancel")) return;
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        foreach (var ext in new[] { ".json", ".enc" })
        {
            var path = Path.Combine(dir, _activeSlot + ext);
            if (File.Exists(path)) File.Delete(path);
        }
        DiscoverSlots();
        _activeSlot = _availableSlots[0];
        LoadSlotData(_activeSlot);
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
        foreach (var d in _defaults)      { if (seen.Add(d.Key)) allKeys.Add(d.Key); }
        foreach (var k in _slotData.Keys) { if (seen.Add(k))     allKeys.Add(k); }

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

            var defEntry = _defaults.Find(e => e.Key == key);
            _slotData.TryGetValue(key, out var currentToken);
            var displayType = defEntry?.Type ?? (currentToken != null ? InferFromToken(currentToken).Item1 : DSMDataType.String);
            var currentStr  = currentToken != null ? InferFromToken(currentToken).Item2 : (defEntry?.SerializedDefault ?? string.Empty);

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
                    if (newType != defEntry.Type) { defEntry.Type = newType; _defaultsDirty = true; }

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
                        _defaultsDirty = true;
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
                    var jObj = ReadSlotJObject(_activeSlot) ?? new JObject();
                    jObj[key] = EntryToJToken(new DSMDataEntry { Key = key, Type = displayType, SerializedDefault = newCurrent });
                    WriteSlotJObject(_activeSlot, jObj);
                    _slotData[key] = jObj[key]!;
                }
            }
        }

        if (toRemoveKey != null)
        {
            if (EditorUtility.DisplayDialog("Remove Entry",
                $"Remove '{toRemoveKey}' from DSMConstant and '{_activeSlot}' slot?", "Remove", "Cancel"))
            {
                _defaults.RemoveAll(e => e.Key == toRemoveKey);
                _defaultsDirty = true;
                PropagateToAllSlots(jObj => jObj.Remove(toRemoveKey));
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

        var keyExists = _defaults.Exists(e => e.Key == _newKey.Trim());
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
                CommitNewEntry();
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
        var statusLabel = _defaultsDirty
            ? $"{_defaults.Count} defaults  ·  {_slotData.Count} in [{_activeSlot}]  ·  ● unsaved changes"
            : $"{_defaults.Count} defaults  ·  {_slotData.Count} in [{_activeSlot}]";
        GUILayout.Label(statusLabel, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(_defaults.Count == 0 || !_defaultsDirty))
        {
            if (GUILayout.Button("Save DSMConstant.cs", GUILayout.Height(26), GUILayout.Width(170)))
            {
                DSMCodeGenerator.Generate(_defaults);
                _defaultsDirty = false;
            }
        }
    }

    // ── Config helpers ────────────────────────────────────────────────────────

    private UnityEditor.SerializedProperty ConfigProp(string backingField) =>
        _configSO!.FindProperty(backingField);

    private void SetDefaultSlot(string slotName)
    {
        if (_configSO == null) return;
        _configSO.Update();
        ConfigProp("_defaultSlot").stringValue = slotName;
        _configSO.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Repaint();
    }

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
        _newSerializedDefault = GetTypeDefault(_newType);
    }

    private void CommitNewEntry()
    {
        var key = _newKey.Trim();
        _defaults.Add(new DSMDataEntry { Key = key, Type = _newType, SerializedDefault = _newSerializedDefault });
        _defaultsDirty = true;
        var token = EntryToJToken(new DSMDataEntry { Key = key, Type = _newType, SerializedDefault = _newSerializedDefault });
        PropagateToAllSlots(jObj => jObj[key] = token);
    }

    private void PropagateToAllSlots(Action<JObject> mutate)
    {
        foreach (var slotName in _availableSlots)
        {
            var jObj = ReadSlotJObject(slotName) ?? new JObject();
            mutate(jObj);
            WriteSlotJObject(slotName, jObj);
        }
        LoadSlotData(_activeSlot);
    }

    private static string GetTypeDefault(DSMDataType type) => type switch
    {
        DSMDataType.Bool => "False", DSMDataType.Int => "0", DSMDataType.Float => "0",
        DSMDataType.Double => "0",   DSMDataType.Long => "0", DSMDataType.String => string.Empty,
        DSMDataType.Vector2 => "0,0", DSMDataType.Vector3 => "0,0,0",
        DSMDataType.Vector4 => "0,0,0,0", DSMDataType.Color => "1,1,1,1",
        _ => string.Empty
    };

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
