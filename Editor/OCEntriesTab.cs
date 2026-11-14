#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Entries tab: the Config's Entry Definitions in Runtime Panel order, plus Add Entry and Reset All Values.</summary>
    internal sealed class OCEntriesTab : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly List<int> _visible = new();
        private readonly HashSet<int> _expanded = new();
        private readonly List<OCEntryRow> _rows = new();
        private readonly ListView _list;
        private readonly VisualElement _header;
        private readonly HelpBox _duplicates;
        private readonly TextField _newKey;
        private readonly EnumField _newType;
        private readonly Button _add;
        private readonly Label _addError;
        private readonly Label _overrides;
        private int _shownCount = -1;

        public OCEntriesTab(OCManagerContext context)
        {
            _context = context;
            AddToClassList("oc-tab");

            _duplicates = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            _duplicates.AddToClassList("oc-banner");
            Add(_duplicates);

            var header = _header = new VisualElement();
            header.AddToClassList("oc-entry__main");
            header.AddToClassList("oc-list-header");
            header.Add(HeaderCell("oc-col-type", "Type"));
            header.Add(HeaderCell("oc-col-key", "Key / Label"));
            header.Add(HeaderCell("oc-col-default", "Default"));
            header.Add(HeaderCell("oc-col-value", "Value"));
            header.Add(HeaderCell("oc-col-reset", string.Empty));
            Add(header);

            _list = new ListView
            {
                itemsSource = _visible,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.None,
                showBoundCollectionSize = false,
                makeItem = () =>
                {
                    var row = new OCEntryRow(_context, i => _expanded.Contains(i), ToggleExpanded, Rebuild);
                    _rows.Add(row);
                    return row;
                },
                bindItem = (element, i) => ((OCEntryRow)element).Bind(_visible[i]),
                unbindItem = (element, _) => ((OCEntryRow)element).Bind(-1)
            };
            _list.AddToClassList("oc-entry-list");
            _list.itemIndexChanged += OnReordered;
            Add(_list);

            var footer = new VisualElement();
            footer.AddToClassList("oc-footer");
            _newKey = new TextField { tooltip = "Key of the new Entry" };
            _newKey.textEdition.placeholder = "New key";
            _newKey.AddToClassList("oc-new-key");
            _newKey.RegisterValueChangedCallback(_ => Refresh());
            _newType = new EnumField(OCDataType.String);
            _newType.AddToClassList("oc-new-type");
            _add = new Button(AddEntry) { text = "Add Entry" };
            _add.AddToClassList("oc-button--primary");
            _addError = new Label();
            _addError.AddToClassList("oc-error-text");
            var spacer = new VisualElement();
            spacer.AddToClassList("oc-grow");
            _overrides = new Label();
            _overrides.AddToClassList("oc-muted");
            var resetAll = new Button(ResetAll) { text = "Reset All Values" };
            resetAll.AddToClassList("oc-button--danger");
            footer.Add(_newKey);
            footer.Add(_newType);
            footer.Add(_add);
            footer.Add(_addError);
            footer.Add(spacer);
            footer.Add(_overrides);
            footer.Add(resetAll);
            Add(footer);

            Rebuild();
        }

        /// <summary>Rebuilds the list after Entries were added, removed or reordered, or the search changed.</summary>
        public void Rebuild()
        {
            var entries = _context.Entries;
            var filter = _context.Search.Trim();
            _visible.Clear();
            for (var i = 0; i < entries.arraySize; i++)
                if (Matches(entries.GetArrayElementAtIndex(i), filter)) _visible.Add(i);

            _shownCount = entries.arraySize;
            _list.reorderable = filter.Length == 0;
            _header.EnableInClassList("oc-list-header--no-handle", filter.Length > 0);
            _list.Rebuild();
            Refresh();
        }

        public void Refresh()
        {
            if (_context.Entries.arraySize != _shownCount)
            {
                Rebuild();
                return;
            }

            foreach (var row in _rows)
                if (row.Index >= 0) row.Refresh();

            var duplicates = _context.DuplicateKeys;
            _duplicates.text = $"Duplicate keys: {string.Join(", ", duplicates)}";
            _duplicates.style.display = duplicates.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            var error = OCEntryRules.ValidateNewKey(_context.Config, _newKey.value);
            _add.SetEnabled(error == null);
            _addError.text = _newKey.value.Length > 0 ? error ?? string.Empty : string.Empty;
            _overrides.text = $"Overrides: {OC.Store.OverrideKeys.Count}";
        }

        private static bool Matches(SerializedProperty element, string filter)
        {
            if (filter.Length == 0) return true;
            return element.FindPropertyRelative("_key").stringValue.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   element.FindPropertyRelative("_label").stringValue.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ToggleExpanded(int index)
        {
            if (!_expanded.Remove(index)) _expanded.Add(index);
            var position = _visible.IndexOf(index);
            if (position >= 0) _list.RefreshItem(position);
        }

        private void OnReordered(int from, int to)
        {
            _context.Entries.MoveArrayElement(from, to);
            _context.So.ApplyModifiedProperties();

            var flags = new List<bool>(_shownCount);
            for (var i = 0; i < _shownCount; i++) flags.Add(_expanded.Contains(i));
            var moved = flags[from];
            flags.RemoveAt(from);
            flags.Insert(to, moved);
            _expanded.Clear();
            for (var i = 0; i < flags.Count; i++)
                if (flags[i]) _expanded.Add(i);

            Rebuild();
        }

        private void AddEntry()
        {
            var key = _newKey.value.Trim();
            if (OCEntryRules.ValidateNewKey(_context.Config, key) != null) return;
            var type = (OCDataType)_newType.value;

            var entriesProp = _context.Entries;
            var index = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(index);
            var element = entriesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_key").stringValue = key;
            element.FindPropertyRelative("_type").enumValueIndex = (int)type;
            element.FindPropertyRelative("_label").stringValue = string.Empty;
            element.FindPropertyRelative("_exposed").boolValue = false;
            element.FindPropertyRelative("_defaultJson").stringValue = OCEntryDefinition.DefaultJsonFor(type);
            element.FindPropertyRelative("_hasRange").boolValue = false;
            element.FindPropertyRelative("_rangeMin").floatValue = 0f;
            element.FindPropertyRelative("_rangeMax").floatValue = 0f;
            element.FindPropertyRelative("_hasHeader").boolValue = false;
            element.FindPropertyRelative("_header").stringValue = string.Empty;
            element.FindPropertyRelative("_hasOptions").boolValue = false;
            element.FindPropertyRelative("_options").ClearArray();
            element.FindPropertyRelative("_audienceIds").ClearArray();
            _context.So.ApplyModifiedProperties();

            _newKey.SetValueWithoutNotify(string.Empty);
            _expanded.Add(index);
            Rebuild();
            _list.ScrollToItem(_visible.Count - 1);
        }

        private static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("Reset All Values", "Reset all overrides to their defaults?", "Reset", "Cancel")) return;
            OC.ResetAll();
            if (!EditorApplication.isPlaying) OC.Save();
        }

        private static VisualElement HeaderCell(string column, string text)
        {
            var cell = new Label(text);
            cell.AddToClassList("oc-cell");
            cell.AddToClassList(column);
            return cell;
        }
    }
}
