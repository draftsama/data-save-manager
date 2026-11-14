#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Entry Action buttons at the end of one widget row. While any of them runs, the whole row is locked.</summary>
    internal sealed class OCEntryActionBar : IOCLayoutAware
    {
        private const string RunningText = "Running…";

        private readonly List<OCRegisteredEntryAction> _actions = new();
        private readonly List<Button> _buttons = new();
        private readonly List<string> _titles = new();
        private readonly VisualElement _row;
        private readonly OCEntryActionError _error;

        public OCEntryActionBar(
            VisualElement row,
            VisualElement buttonParent,
            IReadOnlyList<OCRegisteredEntryAction> actions,
            Func<OCRegisteredEntryAction, string> displayTitle,
            Action<OCRegisteredEntryAction> onRun,
            OCEntryActionError error)
        {
            _row = row;
            _error = error;
            foreach (var action in actions)
            {
                var title = displayTitle(action);
                var button = new Button(() =>
                {
                    if (!AnyRunning()) onRun(action);
                }) { text = title, tooltip = title };
                button.AddToClassList("ocp-button");
                buttonParent.Add(button);
                _actions.Add(action);
                _buttons.Add(button);
                _titles.Add(title);
            }

            row.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Unsubscribe();
                foreach (var action in _actions) action.StatusChanged += OnStatusChanged;
                Refresh();
            });
            row.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            Refresh();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            foreach (var button in _buttons)
            {
                button.style.width = layout.EntryActionButtonWidth;
                button.style.height = layout.RowHeight - 6f;
                button.style.flexShrink = 0f;
            }
            _error.ApplyLayout(layout);
        }

        private bool AnyRunning()
        {
            foreach (var action in _actions)
                if (action.IsRunning) return true;
            return false;
        }

        private void OnStatusChanged(OCRegisteredEntryAction action) => Refresh();

        private void Refresh()
        {
            _row.SetEnabled(!AnyRunning());

            string? lastError = null;
            for (var i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                _buttons[i].text = action.IsRunning ? RunningText : _titles[i];
                if (action.Status == OCCommandStatus.Failed && !string.IsNullOrEmpty(action.LastError))
                    lastError = action.LastError;
            }
            _error.SetError(lastError);
        }

        private void Unsubscribe()
        {
            foreach (var action in _actions) action.StatusChanged -= OnStatusChanged;
        }
    }
}
