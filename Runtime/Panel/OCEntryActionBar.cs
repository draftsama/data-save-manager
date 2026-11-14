#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    /// <summary>The Entry Action buttons at the end of one widget row. While any of them runs, the whole row is locked.</summary>
    public sealed class OCEntryActionBar : MonoBehaviour, IOCLayoutAware
    {
        [SerializeField] private Button? _buttonTemplate;
        [SerializeField] private string _runningText = "Running…";

        private readonly List<OCRegisteredEntryAction> _actions = new();
        private readonly List<Button> _buttons = new();
        private readonly List<TMP_Text?> _labels = new();
        private readonly List<string> _titles = new();
        private CanvasGroup? _rowGroup;
        private OCEntryActionError? _error;

        public void Setup(
            IReadOnlyList<OCRegisteredEntryAction> actions,
            Func<OCRegisteredEntryAction, string> displayTitle,
            Action<OCRegisteredEntryAction> onRun,
            CanvasGroup rowGroup,
            OCEntryActionError? error)
        {
            Unsubscribe();
            foreach (var button in _buttons) Destroy(button.gameObject);
            _actions.Clear();
            _buttons.Clear();
            _labels.Clear();
            _titles.Clear();

            _rowGroup = rowGroup;
            _error = error;
            if (_buttonTemplate == null) return;
            _buttonTemplate.gameObject.SetActive(false);

            foreach (var action in actions)
            {
                var button = Instantiate(_buttonTemplate, transform);
                button.gameObject.SetActive(true);
                button.name = action.Title;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                var title = displayTitle(action);
                button.onClick.AddListener(() =>
                {
                    if (!AnyRunning()) onRun(action);
                });

                _actions.Add(action);
                _buttons.Add(button);
                _labels.Add(label);
                _titles.Add(title);
                action.StatusChanged += OnStatusChanged;
            }
            Refresh();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            foreach (var button in _buttons)
            {
                var element = button.TryGetComponent<LayoutElement>(out var e) ? e : button.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = layout.EntryActionButtonWidth;
                element.flexibleWidth = 0f;
            }
            foreach (var label in _labels)
                if (label != null) label.fontSize = layout.FontSize;
        }

        private bool AnyRunning()
        {
            foreach (var action in _actions)
                if (action.IsRunning) return true;
            return false;
        }

        private void OnStatusChanged(OCRegisteredEntryAction action)
        {
            if (this != null) Refresh();
        }

        private void Refresh()
        {
            var running = AnyRunning();
            if (_rowGroup != null) _rowGroup.interactable = !running;

            string? lastError = null;
            for (var i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                var label = _labels[i];
                if (label != null)
                {
                    label.text = action.IsRunning ? _runningText : _titles[i];
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                }
                if (action.Status == OCCommandStatus.Failed && !string.IsNullOrEmpty(action.LastError))
                    lastError = action.LastError;
            }
            if (_error != null) _error.SetError(lastError);
        }

        private void Unsubscribe()
        {
            foreach (var action in _actions) action.StatusChanged -= OnStatusChanged;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
