#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    /// <summary>A Runtime Panel row for one Registered Command: Title, Description, a dot coloured by its last outcome, and a Run button.</summary>
    public sealed class OCCommandRow : MonoBehaviour, IOCLayoutAware
    {
        [SerializeField] private TMP_Text? _title;
        [SerializeField] private TMP_Text? _description;
        [SerializeField] private Image? _statusDot;
        [SerializeField] private TMP_Text? _error;
        [SerializeField] private Button? _runButton;
        [SerializeField] private TMP_Text? _runLabel;
        [SerializeField] private string _runText = "Run";
        [SerializeField] private string _runningText = "Running…";
        [SerializeField] private Color _runningColor = new(0.88f, 0.70f, 0.25f, 1f);
        [SerializeField] private Color _succeededColor = new(0.25f, 0.70f, 0.50f, 1f);
        [SerializeField] private Color _failedColor = new(0.89f, 0.34f, 0.30f, 1f);

        private OCRegisteredCommand? _command;
        private Action<OCRegisteredCommand>? _onRun;

        public void Setup(OCRegisteredCommand command, string displayTitle, Action<OCRegisteredCommand> onRun)
        {
            if (_command != null) _command.StatusChanged -= OnStatusChanged;

            _command = command;
            _onRun = onRun;

            if (_title != null) _title.text = displayTitle;
            if (_description != null)
            {
                _description.text = command.Description ?? string.Empty;
                _description.gameObject.SetActive(command.Description != null);
            }

            if (_runButton != null)
            {
                _runButton.onClick.RemoveListener(OnRunClicked);
                _runButton.onClick.AddListener(OnRunClicked);
            }

            command.StatusChanged += OnStatusChanged;
            Refresh();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            var row = TryGetComponent<LayoutElement>(out var e) ? e : gameObject.AddComponent<LayoutElement>();
            row.minHeight = layout.RowHeight;

            if (_title != null) _title.fontSize = layout.FontSize;
            if (_runLabel != null) _runLabel.fontSize = layout.FontSize;
            if (_description != null) _description.fontSize = layout.CommandDescriptionFontSize;
            if (_error != null) _error.fontSize = layout.CommandDescriptionFontSize;

            if (_runButton != null)
            {
                var button = _runButton.TryGetComponent<LayoutElement>(out var b) ? b : _runButton.gameObject.AddComponent<LayoutElement>();
                button.preferredWidth = layout.CommandButtonWidth;
                button.preferredHeight = layout.RowHeight;
            }
        }

        private void OnRunClicked()
        {
            if (_command != null && !_command.IsRunning) _onRun?.Invoke(_command);
        }

        private void OnStatusChanged(OCRegisteredCommand command)
        {
            if (this != null) Refresh();
        }

        private void Refresh()
        {
            if (_command == null) return;

            var status = _command.Status;
            if (_statusDot != null)
            {
                _statusDot.enabled = status != OCCommandStatus.NeverRun;
                _statusDot.color = status switch
                {
                    OCCommandStatus.Running   => _runningColor,
                    OCCommandStatus.Succeeded => _succeededColor,
                    OCCommandStatus.Failed    => _failedColor,
                    _                          => _statusDot.color
                };
            }

            if (_error != null)
            {
                var failed = status == OCCommandStatus.Failed && !string.IsNullOrEmpty(_command.LastError);
                _error.text = failed ? _command.LastError : string.Empty;
                _error.gameObject.SetActive(failed);
            }

            var running = status == OCCommandStatus.Running;
            if (_runButton != null) _runButton.interactable = !running;
            if (_runLabel != null) _runLabel.text = running ? _runningText : _runText;
        }

        private void OnDestroy()
        {
            if (_command != null) _command.StatusChanged -= OnStatusChanged;
        }
    }
}
