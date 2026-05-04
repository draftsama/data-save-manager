#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DSM/Config")]
public sealed class DSMConfig : ScriptableObject
{
    [field: SerializeField] public bool AutoSave { get; private set; } = true;
    [field: SerializeField] public float AutoSaveDebounce { get; private set; } = 2f;
    [field: SerializeField] public bool Encrypt { get; private set; }
    [field: SerializeField] public string EncryptionKey { get; private set; } = string.Empty;
    [field: SerializeField] public string SavePath { get; private set; } = string.Empty;
    [field: SerializeField] public string DefaultSlot { get; private set; } = "default";
    [field: SerializeField] public bool PrettyPrint { get; private set; }

    [Serializable]
    public sealed class ExposedEntry
    {
        public string Key = string.Empty;
        public string Label = string.Empty;
        public DSMDataType Type = DSMDataType.String;
    }

    [SerializeField] private List<ExposedEntry> _exposedEntries = new();
    public IReadOnlyList<ExposedEntry> ExposedEntries => _exposedEntries;

    public ExposedEntry? FindExposed(string key) => _exposedEntries.Find(e => e.Key == key);

    public void SetExposed(string key, string label, DSMDataType type)
    {
        var existing = FindExposed(key);
        if (existing != null) { existing.Label = label; existing.Type = type; }
        else _exposedEntries.Add(new ExposedEntry { Key = key, Label = label, Type = type });
    }

    public void RemoveExposed(string key) => _exposedEntries.RemoveAll(e => e.Key == key);
}
