#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The single asset holding every Entry Definition plus save behaviour.</summary>
    [CreateAssetMenu(menuName = "Operator Console/Config")]
    public sealed class OCConfig : ScriptableObject
    {
        [SerializeField] private bool _autoSave = true;
        [SerializeField, Min(0f)] private float _autoSaveDebounce = 1f;
        [SerializeField] private string _saveDirectory = string.Empty;
        [SerializeField] private string _fileName = "save.json";
        [SerializeField] private List<OCEntryDefinition> _entries = new();
        [SerializeField] private List<OCAudience> _audiences = new();
        [SerializeField] private List<OCPanelDefinition> _panels = new();

        public IReadOnlyList<OCAudience> Audiences => _audiences;

        /// <summary>The Panel Definitions the app creates its Runtime Panels from.</summary>
        public IReadOnlyList<OCPanelDefinition> Panels => _panels;

        public bool TryGetAudience(string id, out OCAudience audience)
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

        public bool TryGetAudienceByName(string name, out OCAudience audience)
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
        public IReadOnlyList<OCEntryDefinition> Entries => _entries;

        public bool TryGetEntry(string key, out OCEntryDefinition entry)
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

        public void SetEntry(OCEntryDefinition entry)
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

        internal void AddAudienceForTests(OCAudience audience) => _audiences.Add(audience);

        internal void AddPanelForTests(OCPanelDefinition definition) => _panels.Add(definition);

        internal void SetForTests(bool autoSave, float debounce, string saveDirectory, string fileName)
        {
            _autoSave = autoSave;
            _autoSaveDebounce = debounce;
            _saveDirectory = saveDirectory;
            _fileName = fileName;
        }
    }
}
