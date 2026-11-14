#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Runtime Panel's Log tab: search, type filters with counts, Collapse, Copy all and Clear over a virtualized list of Log Entries.</summary>
    internal sealed class OCLogView : VisualElement, IOCLayoutAware
    {
        private const float RefreshInterval = 0.2f;
        private const float BottomTolerance = 2f;
        private const int TypeCount = 3;
        private const float EstimatedRowHeight = 44f;

        private readonly OCLog _log;
        private readonly List<OCLogEntry> _entries = new();
        private readonly List<int> _rows = new();
        private readonly TextField _search = new();
        private readonly Button[] _filters = new Button[TypeCount];
        private readonly Label[] _filterCounts = new Label[TypeCount];
        private readonly int[] _shownCounts = { -1, -1, -1 };
        private readonly bool[] _typeShown = { true, true, true };
        private readonly Toggle _collapse = new();
        private readonly Label _collapseLabel = new("Collapse");
        private readonly Button _copyAll;
        private readonly Button _clear;
        private readonly ListView _list;
        private readonly ScrollView _listScroll;
        private readonly VisualElement _empty = new();
        private readonly Label _emptyTitle = new();
        private readonly Label _emptyText = new();

        private OCPanelLayout? _layout;
        private OCLogEntry? _expanded;
        private string _query = string.Empty;
        private int _shownVersion = -1;
        private float _nextRefresh;

        public OCLogView(OCLog log)
        {
            _log = log;
            AddToClassList("ocp-log");

            var top = new VisualElement();
            top.AddToClassList("ocp-log__toolbar");
            _search.AddToClassList("ocp-log__search");
            _search.textEdition.placeholder = "Search messages and stack traces";
            _search.RegisterValueChangedCallback(e =>
            {
                _query = e.newValue ?? string.Empty;
                Refresh(true);
            });
            top.Add(_search);
            for (var i = 0; i < TypeCount; i++) top.Add(CreateFilter(i));
            Add(top);

            var second = new VisualElement();
            second.AddToClassList("ocp-log__toolbar");
            second.Add(CreateCollapseSwitch());
            var spacer = new VisualElement();
            spacer.AddToClassList("ocp-grow");
            second.Add(spacer);
            _copyAll = ToolbarButton("Copy all", null, CopyAll);
            _clear = ToolbarButton("Clear", "ocp-button--danger", _log.Clear);
            second.Add(_copyAll);
            second.Add(_clear);
            Add(second);

            _list = new ListView(_rows, EstimatedRowHeight, MakeRow, BindRow)
            {
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                horizontalScrollingEnabled = false
            };
            _list.AddToClassList("ocp-log__list");
            _listScroll = _list.Q<ScrollView>();
            Add(_list);

            _empty.AddToClassList("ocp-log__empty");
            _emptyTitle.AddToClassList("ocp-log__empty-title");
            _empty.Add(_emptyTitle);
            _empty.Add(_emptyText);
            Add(_empty);
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            _layout = layout;
            foreach (var button in new[] { _copyAll, _clear })
            {
                button.style.fontSize = layout.FontSize * 0.85f;
                button.style.height = layout.RowHeight;
            }
            _search.style.fontSize = layout.FontSize;
            _search.style.height = layout.RowHeight;
            for (var i = 0; i < TypeCount; i++)
            {
                _filters[i].style.height = layout.RowHeight;
                _filterCounts[i].style.fontSize = layout.FontSize;
            }
            _collapseLabel.style.fontSize = layout.FontSize * 0.85f;
            _emptyTitle.style.fontSize = layout.FontSize;
            _emptyText.style.fontSize = layout.FontSize * 0.8f;
            _listScroll.verticalScroller.style.width = layout.ScrollbarWidth;
            _listScroll.mouseWheelScrollSize = layout.ScrollSensitivity;
            _list.RefreshItems();
        }

        /// <summary>Shows the Log's latest state when it changed, at most a few times a second unless <paramref name="force"/>.</summary>
        public void Refresh(bool force = false)
        {
            var version = _log.Version;
            if (!force && (version == _shownVersion || Time.unscaledTime < _nextRefresh)) return;
            _shownVersion = version;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            var scroller = _listScroll.verticalScroller;
            var atBottom = scroller.value >= scroller.highValue - BottomTolerance;

            _log.CopyTo(_entries);
            if (_expanded != null && !_entries.Contains(_expanded)) _expanded = null;
            BuildRows();
            UpdateCounts();
            _list.RefreshItems();
            if (atBottom && _rows.Count > 0) _list.ScrollToItem(-1);
        }

        private void BuildRows()
        {
            _rows.Clear();
            var collapse = _collapse.value;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!_typeShown[(int)entry.Type]) continue;
                if (_query.Length > 0 && !OCLogMarkup.Matches(entry.Message, _query) && !OCLogMarkup.Matches(entry.StackTrace, _query)) continue;
                var repeats = collapse ? 1 : entry.Count;
                for (var r = 0; r < repeats; r++) _rows.Add(i);
            }

            var hasEntries = _entries.Count > 0;
            var shown = _rows.Count > 0;
            _list.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
            if (shown) return;
            _emptyTitle.text = hasEntries ? "No matches" : "No log entries";
            _emptyText.text = hasEntries
                ? "Change the search or turn a filter back on."
                : "Messages from Debug.Log, warnings and errors appear here as the app runs.";
        }

        private void UpdateCounts()
        {
            SetCount(0, _log.InfoCount);
            SetCount(1, _log.WarningCount);
            SetCount(2, _log.ErrorCount);
        }

        private void SetCount(int type, int count)
        {
            if (_shownCounts[type] == count) return;
            _shownCounts[type] = count;
            _filterCounts[type].text = count.ToString();
        }

        private VisualElement MakeRow() => new OCLogRow(ToggleExpanded);

        private void BindRow(VisualElement element, int index)
        {
            if (_layout == null || index < 0 || index >= _rows.Count) return;
            var entry = _entries[_rows[index]];
            var firstOfEntry = index == 0 || _rows[index - 1] != _rows[index];
            ((OCLogRow)element).Bind(entry, _collapse.value, firstOfEntry && ReferenceEquals(entry, _expanded), _query, _layout);
        }

        private void ToggleExpanded(OCLogEntry entry)
        {
            _expanded = ReferenceEquals(_expanded, entry) ? null : entry;
            _list.RefreshItems();
        }

        private Button CreateFilter(int type)
        {
            var dot = new VisualElement();
            dot.AddToClassList("ocp-log-dot");
            dot.AddToClassList(type switch { 0 => "ocp-log-dot--info", 1 => "ocp-log-dot--warning", _ => "ocp-log-dot--error" });
            var count = new Label("0");
            count.AddToClassList("ocp-log__filter-count");
            _filterCounts[type] = count;

            var button = new Button(() =>
            {
                _typeShown[type] = !_typeShown[type];
                _filters[type].EnableInClassList("ocp-log__filter--on", _typeShown[type]);
                Refresh(true);
            });
            button.AddToClassList("ocp-log__filter");
            button.AddToClassList("ocp-log__filter--on");
            button.tooltip = type switch { 0 => "Info", 1 => "Warnings", _ => "Errors" };
            button.Add(dot);
            button.Add(count);
            _filters[type] = button;
            return button;
        }

        private VisualElement CreateCollapseSwitch()
        {
            var row = new VisualElement();
            row.AddToClassList("ocp-log__collapse");
            _collapseLabel.AddToClassList("ocp-log__collapse-label");
            row.Add(_collapseLabel);

            _collapse.AddToClassList("ocp-switch");
            _collapse.SetValueWithoutNotify(true);
            var knob = new VisualElement();
            knob.AddToClassList("ocp-switch-knob");
            var checkmark = _collapse.Q(className: Toggle.checkmarkUssClassName);
            checkmark.Add(knob);
            // The default theme's checked-checkmark image rule outranks any USS selector here.
            checkmark.style.backgroundImage = StyleKeyword.None;
            var input = _collapse.Q(className: Toggle.inputUssClassName);
            input.style.flexBasis = StyleKeyword.Auto;
            input.style.flexShrink = 0f;
            _collapse.RegisterValueChangedCallback(_ => Refresh(true));
            row.Add(_collapse);
            return row;
        }

        private static Button ToolbarButton(string text, string? variant, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ocp-button");
            if (variant != null) button.AddToClassList(variant);
            return button;
        }

        private void CopyAll()
        {
            var text = new StringBuilder();
            foreach (var index in _rows)
                text.AppendLine(OCLogRow.Describe(_entries[index]));
            GUIUtility.systemCopyBuffer = text.ToString();
        }
    }
}
