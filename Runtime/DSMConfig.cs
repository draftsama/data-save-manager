#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "DSM/Config")]
public sealed class DSMConfig : ScriptableObject
{
    [SerializeField, FormerlySerializedAs("<AutoSave>k__BackingField")]
    private bool _autoSave = true;
    public bool AutoSave => _autoSave;

    [SerializeField, FormerlySerializedAs("<AutoSaveDebounce>k__BackingField")]
    private float _autoSaveDebounce = 2f;
    public float AutoSaveDebounce => _autoSaveDebounce;

    [SerializeField, FormerlySerializedAs("<Encrypt>k__BackingField")]
    private bool _encrypt;
    public bool Encrypt => _encrypt;

    [SerializeField]
    private bool _strictSchema;
    public bool StrictSchema => _strictSchema;

    // NOT serialized — set programmatically to keep secrets out of asset files
    [field: NonSerialized]
    public string EncryptionKey { get; private set; } = string.Empty;

    public void SetEncryptionKey(string key)
    {
        DSMEncryptionKey.Validate(key);
        EncryptionKey = key;
    }

    [SerializeField, FormerlySerializedAs("<SavePath>k__BackingField")]
    private string _savePath = string.Empty;
    public string SavePath => _savePath;

    [SerializeField, FormerlySerializedAs("<DefaultSlot>k__BackingField")]
    private string _defaultSlot = "default";
    public string DefaultSlot => _defaultSlot;

    [SerializeField, FormerlySerializedAs("<PrettyPrint>k__BackingField")]
    private bool _prettyPrint;
    public bool PrettyPrint => _prettyPrint;

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
