#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class DSMSlotManager
{
    private readonly Dictionary<string, DSMSlot> _slots = new();
    private readonly DSMConfig _config;
    private readonly DSMSerializer _serializer = new();
    private DSMSlot _activeSlot;

    private static Type? s_constantType;

    public DSMSlot ActiveSlot => _activeSlot;
    public string SaveDirectory => DSMPaths.GetSaveDirectory(_config.SavePath);

    public DSMSlotManager(DSMConfig config)
    {
        _config = config;
        _activeSlot = GetOrCreateSlot(config.DefaultSlot);
        _activeSlot.Load();
    }

    public void UseSlot(string name)
    {
        _activeSlot = GetOrCreateSlot(name);
        _activeSlot.Load();
    }

    public DSMSlot GetSlot(string name) => GetOrCreateSlot(name);

    public void DeleteSlot(string name)
    {
        _slots.Remove(name);
        var dir = SaveDirectory;
        var jsonPath = Path.Combine(dir, $"{name}.json");
        var encPath = Path.Combine(dir, $"{name}.enc");
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        if (File.Exists(encPath)) File.Delete(encPath);
    }

    public string[] GetAllSlots()
    {
        var dir = SaveDirectory;
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".enc",  StringComparison.OrdinalIgnoreCase))
                names.Add(Path.GetFileNameWithoutExtension(file)!);
        }
        return names.ToArray();
    }

    public void SaveActiveSlot() => _activeSlot.Save();

    private DSMSlot GetOrCreateSlot(string name)
    {
        if (_slots.TryGetValue(name, out var slot)) return slot;
        slot = new DSMSlot(name, _config, _serializer, SaveDirectory, ResolveConstantType());
        _slots[name] = slot;
        return slot;
    }

    private static Type? ResolveConstantType()
    {
        if (s_constantType != null) return s_constantType;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == "DSMConstant" && t.IsClass && t.IsAbstract && t.IsSealed)
                        return s_constantType = t;
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException) { }
            catch (Exception ex) { Debug.LogWarning($"DSM: {ex.Message}"); }
        }
        return null;
    }
}
