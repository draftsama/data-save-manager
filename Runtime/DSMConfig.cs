#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>The single asset holding every Entry Definition plus save behaviour.</summary>
    [CreateAssetMenu(menuName = "DSM/Config")]
    public sealed class DSMConfig : ScriptableObject
    {
        [SerializeField] private bool _autoSave = true;
        [SerializeField, Min(0f)] private float _autoSaveDebounce = 1f;
        [SerializeField] private string _saveDirectory = string.Empty;
        [SerializeField] private string _fileName = "save.json";
        [SerializeField] private List<DSMEntryDefinition> _entries = new();
        [SerializeField] private List<DSMAudience> _audiences = new();

        public IReadOnlyList<DSMAudience> Audiences => _audiences;

        public bool TryGetAudience(string id, out DSMAudience audience)
        {
            foreach (var a in _audiences)
            {
                if (a.Id != id) continue;
                audience = a;
                return true;
            }
            audience = null!;
            return false;
        }

        public bool TryGetAudienceByName(string name, out DSMAudience audience)
        {
            foreach (var a in _audiences)
            {
                if (!string.Equals(a.Name, name, System.StringComparison.Ordinal)) continue;
                audience = a;
                return true;
            }
            audience = null!;
            return false;
        }

        public bool AutoSave => _autoSave;
        public float AutoSaveDebounce => _autoSaveDebounce;
        public string SaveDirectory => _saveDirectory;
        public string FileName => _fileName;
        public IReadOnlyList<DSMEntryDefinition> Entries => _entries;

        public bool TryGetEntry(string key, out DSMEntryDefinition entry)
        {
            foreach (var e in _entries)
            {
                if (e.Key != key) continue;
                entry = e;
                return true;
            }
            entry = null!;
            return false;
        }

        public void SetEntry(DSMEntryDefinition entry)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key != entry.Key) continue;
                _entries[i] = entry;
                return;
            }
            _entries.Add(entry);
        }

        public bool RemoveEntry(string key) => _entries.RemoveAll(e => e.Key == key) > 0;

        internal void AddAudienceForTests(DSMAudience audience) => _audiences.Add(audience);

        internal void SetForTests(bool autoSave, float debounce, string saveDirectory, string fileName)
        {
            _autoSave = autoSave;
            _autoSaveDebounce = debounce;
            _saveDirectory = saveDirectory;
            _fileName = fileName;
        }
    }
}
