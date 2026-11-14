#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>One row of the Log tab: type dot, time, first line of the message and repeat count; expanded, the full message and stack trace.</summary>
    internal sealed class OCLogRow : VisualElement
    {
        private const string NoStackTraceInfo = "No stack trace. This build does not record one for Log messages.";
        private const string NoStackTraceWarning = "No stack trace. This build does not record one for Warning messages.";
        private const string NoStackTraceError = "No stack trace.";
        private const float DetailMaxLines = 16f;

        private readonly VisualElement _card = new();
        private readonly VisualElement _main = new();
        private readonly VisualElement _dot = new();
        private readonly Label _time = new();
        private readonly Label _message = new();
        private readonly Label _count = new();
        private readonly VisualElement _detail = new();
        private readonly ScrollView _detailScroll = new(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
        private readonly Label _fullMessage = new();
        private readonly Label _stack = new();
        private readonly Button _copy;
        private int _shownCount = -1;

        public OCLogRow(Action<OCLogEntry> onToggle)
        {
            AddToClassList("ocp-log-row");
            _card.AddToClassList("ocp-log-row__card");
            _main.AddToClassList("ocp-log-row__main");
            _dot.AddToClassList("ocp-log-dot");
            _time.AddToClassList("ocp-log-row__time");
            _message.AddToClassList("ocp-log-row__message");
            _count.AddToClassList("ocp-log-row__count");
            _main.Add(_dot);
            _main.Add(_time);
            _main.Add(_message);
            _main.Add(_count);
            _main.RegisterCallback<ClickEvent>(_ =>
            {
                if (Entry != null) onToggle(Entry);
            });

            _detail.AddToClassList("ocp-log-row__detail");
            _fullMessage.AddToClassList("ocp-log-row__full");
            _fullMessage.selection.isSelectable = true;
            _stack.AddToClassList("ocp-log-row__stack");
            _stack.selection.isSelectable = true;
            _copy = new Button(CopyEntry) { text = "Copy entry" };
            _copy.AddToClassList("ocp-button");
            _copy.AddToClassList("ocp-log-row__copy");
            _detailScroll.AddToClassList("ocp-log-row__detail-scroll");
            _detailScroll.Add(_fullMessage);
            _detailScroll.Add(_stack);
            _detail.Add(_detailScroll);
            _detail.Add(_copy);

            _card.Add(_main);
            _card.Add(_detail);
            Add(_card);
        }

        public OCLogEntry? Entry { get; private set; }

        public void Bind(OCLogEntry entry, bool showCount, bool expanded, OCPanelLayout layout)
        {
            Entry = entry;
            EnableInClassList("ocp-log-row--info", entry.Type == OCLogType.Info);
            EnableInClassList("ocp-log-row--warning", entry.Type == OCLogType.Warning);
            EnableInClassList("ocp-log-row--error", entry.Type == OCLogType.Error);
            EnableInClassList("ocp-log-row--expanded", expanded);

            _time.text = entry.TimeText;
            _message.text = entry.Preview;
            var count = showCount ? entry.Count : 1;
            if (count != _shownCount)
            {
                _shownCount = count;
                _count.text = count > 1 ? "×" + count : string.Empty;
            }
            _count.style.display = count > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            _detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (expanded)
            {
                _fullMessage.text = entry.Message;
                _stack.text = entry.HasStackTrace ? entry.StackTrace.TrimEnd() : NoStackTrace(entry.Type);
                _stack.EnableInClassList("ocp-log-row__stack--none", !entry.HasStackTrace);
            }

            style.paddingBottom = layout.RowSpacing;
            _main.style.minHeight = layout.RowHeight;
            _time.style.fontSize = layout.FontSize * 0.8f;
            _message.style.fontSize = layout.FontSize;
            _count.style.fontSize = layout.FontSize * 0.75f;
            _fullMessage.style.fontSize = layout.LogStackTraceFontSize;
            _stack.style.fontSize = layout.LogStackTraceFontSize;
            _copy.style.fontSize = layout.LogStackTraceFontSize;
            // A ListView row taller than the list keeps its dynamic-height layout from settling, so a long stack trace scrolls inside the row.
            _detailScroll.style.maxHeight = layout.LogStackTraceFontSize * DetailMaxLines;
        }

        internal static string Describe(OCLogEntry entry)
        {
            var head = $"[{entry.TimeText}] {entry.Type}: {entry.Message}";
            return entry.HasStackTrace ? head + "\n" + entry.StackTrace.TrimEnd() : head;
        }

        private static string NoStackTrace(OCLogType type) => type switch
        {
            OCLogType.Info    => NoStackTraceInfo,
            OCLogType.Warning => NoStackTraceWarning,
            _                  => NoStackTraceError
        };

        private void CopyEntry()
        {
            if (Entry != null) GUIUtility.systemCopyBuffer = Describe(Entry);
        }
    }
}
