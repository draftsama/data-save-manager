#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Commands tab: in Play mode, every Registered Command with its status and a Run button.</summary>
    internal sealed class OCCommandsTab : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly ScrollView _scroll = new();
        private readonly List<(OCRegisteredCommand Command, VisualElement Card, Label Status, Label Error, Button Run)> _cards = new();
        private bool _shownPlaying;
        private int _shownCount = -1;

        public OCCommandsTab(OCManagerContext context)
        {
            _context = context;
            AddToClassList("oc-tab");
            _scroll.AddToClassList("oc-scroll");
            Add(_scroll);
            Rebuild();
        }

        private void Rebuild()
        {
            _scroll.Clear();
            _cards.Clear();
            _shownPlaying = EditorApplication.isPlaying;
            _shownCount = _shownPlaying ? OC.Commands.Commands.Count : -1;

            if (!_shownPlaying)
            {
                _scroll.Add(Notice("Commands register while the app runs. Enter Play mode to see and run them."));
                return;
            }

            var registry = OC.Commands;
            if (registry.Commands.Count == 0)
            {
                _scroll.Add(Notice("No Commands are registered. An owner calls OC.Commands.Register(this) in OnEnable."));
                return;
            }

            foreach (var command in registry.Commands)
            {
                var card = new VisualElement();
                card.AddToClassList("oc-card");
                card.AddToClassList("oc-command");

                var texts = new VisualElement();
                texts.AddToClassList("oc-grow");
                var title = new Label(registry.DisplayTitle(command));
                title.AddToClassList("oc-strong");
                texts.Add(title);
                var owner = new Label(command.Description ?? command.OwnerName);
                owner.AddToClassList("oc-muted");
                owner.AddToClassList("oc-wrap");
                texts.Add(owner);
                var error = new Label();
                error.AddToClassList("oc-error-text");
                error.AddToClassList("oc-wrap");
                texts.Add(error);

                var status = new Label();
                status.AddToClassList("oc-command__status");
                var run = new Button(() => Run(command));
                run.AddToClassList("oc-button--primary");
                run.AddToClassList("oc-command__run");

                card.Add(texts);
                card.Add(status);
                card.Add(run);
                _scroll.Add(card);
                _cards.Add((command, card, status, error, run));
            }
        }

        public void Refresh()
        {
            var playing = EditorApplication.isPlaying;
            if (playing != _shownPlaying || (playing && OC.Commands.Commands.Count != _shownCount))
            {
                Rebuild();
                return;
            }

            var viewAsIds = _context.ViewAsIds();
            foreach (var (command, card, status, error, run) in _cards)
            {
                card.EnableInClassList("oc-entry--dimmed", viewAsIds != null && !OCAudienceFilter.Shows(command, viewAsIds, _context.Config));
                status.text = command.Status switch
                {
                    OCCommandStatus.Running => "◌",
                    OCCommandStatus.Succeeded => "✓",
                    OCCommandStatus.Failed => "✗",
                    _ => string.Empty
                };
                var failed = command.Status == OCCommandStatus.Failed && command.LastError != null;
                error.text = failed ? $"Failed: {command.LastError}" : string.Empty;
                error.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
                card.EnableInClassList("oc-card--error", failed);
                run.text = command.IsRunning ? "Running…" : "Run";
                run.SetEnabled(!command.IsRunning);
            }
        }

        private static void Run(OCRegisteredCommand command)
        {
            if (command.Confirm
                && !EditorUtility.DisplayDialog("Run Command", $"Run “{OC.Commands.DisplayTitle(command)}”?", "Run", "Cancel"))
                return;
            command.Run();
        }

        private static VisualElement Notice(string text)
        {
            var notice = new HelpBox(text, HelpBoxMessageType.Info);
            notice.AddToClassList("oc-banner");
            return notice;
        }
    }
}
