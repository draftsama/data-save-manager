#nullable enable

using System;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>A Runtime Panel row for one Registered Command: Title, Description, a dot coloured by its last outcome, and a Run button.</summary>
    internal sealed class OCCommandRow : VisualElement, IOCLayoutAware
    {
        private const string RunText = "Run";
        private const string RunningText = "Running…";

        private readonly OCRegisteredCommand _command;
        private readonly VisualElement _dot = new();
        private readonly Label _title;
        private readonly Label _description;
        private readonly Label _error = new();
        private readonly Button _run;

        public OCCommandRow(OCRegisteredCommand command, string displayTitle, Action<OCRegisteredCommand> onRun)
        {
            _command = command;
            AddToClassList("ocp-command");

            _dot.AddToClassList("ocp-status-dot");
            Add(_dot);

            var text = new VisualElement();
            text.AddToClassList("ocp-command__text");
            _title = new Label(displayTitle);
            _title.AddToClassList("ocp-command__title");
            _description = new Label(command.Description ?? string.Empty);
            _description.AddToClassList("ocp-command__description");
            _description.style.display = command.Description != null ? DisplayStyle.Flex : DisplayStyle.None;
            _error.AddToClassList("ocp-error");
            text.Add(_title);
            text.Add(_description);
            text.Add(_error);
            Add(text);

            _run = new Button(() =>
            {
                if (!_command.IsRunning) onRun(_command);
            }) { text = RunText };
            _run.AddToClassList("ocp-button");
            _run.AddToClassList("ocp-button--primary");
            Add(_run);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _command.StatusChanged -= OnStatusChanged;
                _command.StatusChanged += OnStatusChanged;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _command.StatusChanged -= OnStatusChanged);
            Refresh();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            style.minHeight = layout.RowHeight;
            _title.style.fontSize = layout.FontSize;
            _run.style.fontSize = layout.FontSize;
            _description.style.fontSize = layout.CommandDescriptionFontSize;
            _error.style.fontSize = layout.CommandDescriptionFontSize;
            _run.style.width = layout.CommandButtonWidth;
            _run.style.height = layout.RowHeight;
        }

        private void OnStatusChanged(OCRegisteredCommand command) => Refresh();

        private void Refresh()
        {
            var status = _command.Status;
            _dot.style.visibility = status == OCCommandStatus.NeverRun ? Visibility.Hidden : Visibility.Visible;
            _dot.EnableInClassList("ocp-status-dot--running", status == OCCommandStatus.Running);
            _dot.EnableInClassList("ocp-status-dot--succeeded", status == OCCommandStatus.Succeeded);
            _dot.EnableInClassList("ocp-status-dot--failed", status == OCCommandStatus.Failed);

            var failed = status == OCCommandStatus.Failed && !string.IsNullOrEmpty(_command.LastError);
            _error.text = failed ? _command.LastError : string.Empty;
            _error.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;

            var running = status == OCCommandStatus.Running;
            _run.SetEnabled(!running);
            _run.text = running ? RunningText : RunText;
        }
    }
}
