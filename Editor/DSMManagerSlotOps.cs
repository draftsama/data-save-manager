#nullable enable
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class DSMManagerSlotOps
{
    private List<DSMDataEntry> _defaults = new();
    private Dictionary<string, JToken> _slotData = new(StringComparer.Ordinal);
    private string _activeSlot = "default";
    private string[] _availableSlots = Array.Empty<string>();
    private DSMConfig? _config;
    private SerializedObject? _configSO;
    private bool _defaultsDirty;
    private string _lastError = string.Empty;

    public List<DSMDataEntry> Defaults => _defaults;
    public Dictionary<string, JToken> SlotData => _slotData;
    public string[] AvailableSlots => _availableSlots;
    public string ActiveSlot { get => _activeSlot; set => _activeSlot = value; }
    public bool DefaultsDirty { get => _defaultsDirty; set => _defaultsDirty = value; }
    public string LastError => _lastError;

    public void BindConfig(DSMConfig? config, SerializedObject? configSO)
    {
        _config = config;
        _configSO = configSO;
    }

    public void ResetDefaults(List<DSMDataEntry> defaults) => _defaults = defaults;

    public string GetSlotName() =>
        string.IsNullOrEmpty(_config?.DefaultSlot) ? "default" : _config.DefaultSlot;

    public void DiscoverSlots()
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

    public string ResolveSlotPath(string slot)
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        var enc = Path.Combine(dir, $"{slot}.enc");
        return File.Exists(enc) ? enc : Path.Combine(dir, $"{slot}.json");
    }

    public void LoadSlotData(string slot)
    {
        _slotData = new Dictionary<string, JToken>(StringComparer.Ordinal);
        var jObj = ReadSlotJObject(slot);
        if (jObj == null) return;
        foreach (var prop in jObj.Properties())
            _slotData[prop.Name] = prop.Value;
        SyncRuntimeKeys();
    }

    public void SelectSlot(string slot)
    {
        _activeSlot = slot;
        LoadSlotData(_activeSlot);
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

    public JObject? ReadSlotJObject(string slot)
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
            var parsed = JObject.Parse(content);
            ClearError();
            return parsed;
        }
        catch (JsonException ex)
        {
            Fail("parse save data", slot, ex);
            return null;
        }
        catch (IOException ex)
        {
            Fail("read save file", slot, ex);
            return null;
        }
        catch (Exception ex)
        {
            Fail("load", slot, ex);
            return null;
        }
    }

    public void WriteSlotJObject(string slot, JObject data)
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        try
        {
            Directory.CreateDirectory(dir);
            var pretty = _config?.PrettyPrint == true ? Formatting.Indented : Formatting.None;
            var json = data.ToString(pretty);
            if (_config?.Encrypt == true)
                File.WriteAllBytes(Path.Combine(dir, $"{slot}.enc"),
                    DSMEncryptor.Encrypt(json, _config.EncryptionKey));
            else
                File.WriteAllText(Path.Combine(dir, $"{slot}.json"), json, Encoding.UTF8);
            ClearError();
        }
        catch (IOException ex)
        {
            Fail("write save file", slot, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            Fail("write save file", slot, ex);
        }
        catch (Exception ex)
        {
            Fail("save", slot, ex);
        }
    }

    public void DeleteActiveSlot()
    {
        var dir = DSMPaths.GetSaveDirectory(_config?.SavePath);
        try
        {
            foreach (var ext in new[] { ".json", ".enc" })
            {
                var path = Path.Combine(dir, _activeSlot + ext);
                if (File.Exists(path)) File.Delete(path);
            }
            ClearError();
        }
        catch (Exception ex)
        {
            Fail("delete", _activeSlot, ex);
            return;
        }
        DiscoverSlots();
        SelectSlot(_availableSlots[0]);
    }

    public void CreateSlot(string slot)
    {
        var jObj = new JObject();
        foreach (var entry in _defaults)
            jObj[entry.Key] = EntryToJToken(entry);
        WriteSlotJObject(slot, jObj);
        DiscoverSlots();
        SelectSlot(slot);
    }

    public void SetDefaultSlot(string slotName)
    {
        if (_configSO == null) return;
        _configSO.Update();
        _configSO.FindProperty("_defaultSlot").stringValue = slotName;
        _configSO.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    public void CommitNewEntry(string key, DSMDataType type, string serializedDefault)
    {
        _defaults.Add(new DSMDataEntry { Key = key, Type = type, SerializedDefault = serializedDefault });
        _defaultsDirty = true;
        var token = EntryToJToken(new DSMDataEntry { Key = key, Type = type, SerializedDefault = serializedDefault });
        PropagateToAllSlots(jObj => jObj[key] = token);
    }

    public void PropagateToAllSlots(Action<JObject> mutate)
    {
        foreach (var slotName in _availableSlots)
        {
            var jObj = ReadSlotJObject(slotName) ?? new JObject();
            mutate(jObj);
            WriteSlotJObject(slotName, jObj);
        }
        LoadSlotData(_activeSlot);
    }

    // Exception messages are never surfaced: Newtonsoft and IO messages can embed save
    // content or paths, and the same no-value-leak rule the runtime follows applies here.
    private void Fail(string operation, string slot, Exception ex)
    {
        _lastError = $"Could not {operation} for slot '{slot}' ({ex.GetType().Name}).";
        Debug.LogError($"DSM Manager: {operation} failed for slot '{slot}' — {ex.GetType().Name}.");
    }

    private void ClearError() => _lastError = string.Empty;

    // ── Type inference from JToken ────────────────────────────────────────────

    public static (DSMDataType, string) InferFromToken(JToken token)
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

    public static JToken EntryToJToken(DSMDataEntry e)
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

    private static float Pf(string[] parts, int i) =>
        i < parts.Length && float.TryParse(parts[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static string Fs(float v) => v.ToString("G", CultureInfo.InvariantCulture);
}

#endif
