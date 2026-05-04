#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class DSMSlotManager
{
    private readonly Dictionary<string, DSMSlot> _slots = new();
    private readonly DSMConfig _config;
    private readonly DSMSerializer _serializer = new();
    private DSMSlot _activeSlot;

    public DSMSlot ActiveSlot => _activeSlot;

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
        var dir = GetSaveDirectory();
        var jsonPath = Path.Combine(dir, $"{name}.json");
        var encPath = Path.Combine(dir, $"{name}.enc");
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        if (File.Exists(encPath)) File.Delete(encPath);
    }

    public string[] GetAllSlots()
    {
        var dir = GetSaveDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        return Directory.GetFiles(dir, "*.json")
            .Concat(Directory.GetFiles(dir, "*.enc"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct()
            .ToArray();
    }

    public void SaveActiveSlot() => _activeSlot.Save();

    private DSMSlot GetOrCreateSlot(string name)
    {
        if (_slots.TryGetValue(name, out var slot)) return slot;
        slot = new DSMSlot(name, _config, _serializer);
        _slots[name] = slot;
        return slot;
    }

    private string GetSaveDirectory() =>
        string.IsNullOrEmpty(_config.SavePath)
            ? Path.Combine(Application.persistentDataPath, "DSM")
            : _config.SavePath;
}
