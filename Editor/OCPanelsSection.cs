#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Runtime Panels tab: edits the Config's Panel Definitions.</summary>
    internal sealed class OCPanelsSection : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly ScrollView _scroll = new();
        private readonly VisualElement _cards = new();
        private readonly List<bool> _expanded = new();
        private readonly List<bool> _layoutExpanded = new();
        private readonly List<Card> _shown = new();
        private string _shownAudiences = string.Empty;
        private int _shownCount = -1;

        private sealed class Card
        {
            public Foldout Foldout = null!;
            public Label Summary = null!;
            public HelpBox DuplicateName = null!;
            public HelpBox? NoAudience;
            public HelpBox KeyWarning = null!;
            public HelpBox Conflicts = null!;
            public HelpBox LockedConflicts = null!;
        }

        public OCPanelsSection(OCManagerContext context)
        {
            _context = context;
            AddToClassList("oc-tab");
            _scroll.AddToClassList("oc-scroll");
            _scroll.Add(_cards);

            var add = new Button(() =>
            {
                var panelsProp = Panels;
                AddDefinition(panelsProp, UniqueName(panelsProp, "Panel"));
                _context.So.ApplyModifiedProperties();
                Refresh();
            }) { text = "+ Add Panel" };
            add.AddToClassList("oc-button--primary");
            add.AddToClassList("oc-actions");
            _scroll.Add(add);
            Add(_scroll);

            // Most Panel Definition edits reach a running Runtime Panel at once; its layout needs applying again.
            this.TrackPropertyValue(Panels, _ =>
            {
                if (!EditorApplication.isPlaying) return;
                foreach (var panel in OC.Panels.All)
                    if (panel != null) panel.ApplySettings();
            });

            Refresh();
        }

        private SerializedProperty Panels => _context.So.FindProperty("_panels");

        public void Refresh()
        {
            var panelsProp = Panels;
            var audiences = string.Join("\n", _context.AudienceNames);
            if (panelsProp.arraySize != _shownCount || audiences != _shownAudiences) Rebuild(panelsProp, audiences);

            var config = _context.Config;
            var duplicates = DuplicateNames(panelsProp);
            for (var i = 0; i < _shown.Count && i < panelsProp.arraySize; i++)
            {
                var card = _shown[i];
                var definition = panelsProp.GetArrayElementAtIndex(i);
                var name = definition.FindPropertyRelative("_name").stringValue;
                var enabled = definition.FindPropertyRelative("_enabled").boolValue;
                card.Foldout.text = name;
                card.Summary.text = i < config.Panels.Count ? Summary(config.Panels[i]) : string.Empty;
                card.Foldout.EnableInClassList("oc-panel-card--disabled", !enabled);
                Show(card.DuplicateName, duplicates.Contains(name));
                if (card.NoAudience != null) Show(card.NoAudience, definition.FindPropertyRelative("_audienceIds").arraySize == 0);
#if ENABLE_INPUT_SYSTEM
                Show(card.KeyWarning, !OCKeyMap.TryGetKey((KeyCode)definition.FindPropertyRelative("_toggleKey").intValue, out _));
#else
                Show(card.KeyWarning, false);
#endif
                var conflicts = i < config.Panels.Count ? TriggerConflicts(config, i, false) : new List<string>();
                card.Conflicts.text = $"Shares an Open Trigger with {string.Join(", ", conflicts)}: one use opens both panels.";
                Show(card.Conflicts, conflicts.Count > 0);
                var locked = i < config.Panels.Count ? TriggerConflicts(config, i, true) : new List<string>();
                card.LockedConflicts.text = $"Has a Panel Lock and shares an Open Trigger with {string.Join(", ", locked)}, which also has one: only one Lock Screen shows at a time, so that use opens only the first of them in this list.";
                Show(card.LockedConflicts, locked.Count > 0);
            }
        }

        private void Rebuild(SerializedProperty panelsProp, string audiences)
        {
            _shownCount = panelsProp.arraySize;
            _shownAudiences = audiences;
            while (_expanded.Count < _shownCount) _expanded.Add(true);
            if (_expanded.Count > _shownCount) _expanded.RemoveRange(_shownCount, _expanded.Count - _shownCount);
            while (_layoutExpanded.Count < _shownCount) _layoutExpanded.Add(false);
            if (_layoutExpanded.Count > _shownCount) _layoutExpanded.RemoveRange(_shownCount, _layoutExpanded.Count - _shownCount);

            _cards.Clear();
            _shown.Clear();
            if (_shownCount == 0)
            {
                var none = new HelpBox("No Panel Definitions: the app shows no Runtime Panel.", HelpBoxMessageType.Info);
                none.AddToClassList("oc-banner");
                _cards.Add(none);
            }

            for (var i = 0; i < _shownCount; i++)
                _cards.Add(BuildCard(panelsProp, i));
        }

        private VisualElement BuildCard(SerializedProperty panelsProp, int index)
        {
            var definition = panelsProp.GetArrayElementAtIndex(index);
            var card = new Card();
            var root = new VisualElement();
            root.AddToClassList("oc-card");
            root.AddToClassList("oc-panel-card");

            card.Foldout = new Foldout { value = _expanded[index] };
            card.Foldout.AddToClassList("oc-panel-card__foldout");
            card.Foldout.RegisterValueChangedCallback(e =>
            {
                if (e.target == card.Foldout && index < _expanded.Count) _expanded[index] = e.newValue;
            });
            root.Add(card.Foldout);

            var remove = new Button(() => Remove(index)) { text = "✕", tooltip = "Remove Panel Definition" };
            remove.AddToClassList("oc-icon-button");
            remove.AddToClassList("oc-card__remove");
            root.Add(remove);

            card.Summary = new Label();
            card.Summary.AddToClassList("oc-panel-card__summary");
            card.Foldout.Q(className: Toggle.inputUssClassName).Add(card.Summary);

            var general = Section(card.Foldout, "General");
            general.Add(Field(definition, "_name", "Name", "Code finds this Runtime Panel by this name: OC.Panels.Get(name)."));
            card.DuplicateName = Warning(general, "Another Panel Definition has this name; OC.Panels.Get returns only the first.", HelpBoxMessageType.Error);
            general.Add(Field(definition, "_enabled", "Enabled", "Disabled Panel Definitions create no Runtime Panel."));
            if (_context.Config.Audiences.Count > 0)
            {
                var mask = OCAudienceEditing.CreateMaskField("Audiences", definition.FindPropertyRelative("_audienceIds"), _context.Config, _context.AudienceNames);
                mask.tooltip = "Exposed Entries this Panel shows: those naming any of these Audiences, plus those naming none. None selected: every Exposed Entry.";
                general.Add(mask);
                card.NoAudience = Warning(general, "This Panel serves no Audience, so it shows every Exposed Entry, including ones meant for other Audiences.", HelpBoxMessageType.Warning);
            }
            general.Add(Field(definition, "_sortOrder", "Sort Order", null));

            var opening = Section(card.Foldout, "Opening");
            opening.Add(Field(definition, "_startVisible", "Start Visible", "Show the panel as soon as it is created."));
            var hotkey = Field(definition, "_hotkeyEnabled", "Hotkey", null);
            var toggleKey = Field(definition, "_toggleKey", "Toggle Key", null);
            opening.Add(hotkey);
            opening.Add(toggleKey);
            EnableWith(opening, definition.FindPropertyRelative("_hotkeyEnabled"), toggleKey);
            card.KeyWarning = Warning(opening, "Not a keyboard key: the hotkey does nothing with the Input System.", HelpBoxMessageType.Error);
            opening.Add(Field(definition, "_cornerTap._enabled", "Corner Tap", "Open the panel by tapping one screen corner several times in quick succession."));
            var cornerFields = new VisualElement();
            cornerFields.AddToClassList("oc-panel-section__indent");
            cornerFields.Add(Field(definition, "_cornerTap._corner", "Corner", null));
            cornerFields.Add(Field(definition, "_cornerTap._sizePercent", "Zone Size %", null));
            cornerFields.Add(Field(definition, "_cornerTap._tapCount", "Tap Count", null));
            cornerFields.Add(Field(definition, "_cornerTap._maxInterval", "Max Interval (s)", null));
            opening.Add(cornerFields);
            EnableWith(opening, definition.FindPropertyRelative("_cornerTap._enabled"), cornerFields);
            card.Conflicts = Warning(opening, string.Empty, HelpBoxMessageType.Warning);
            card.LockedConflicts = Warning(opening, string.Empty, HelpBoxMessageType.Warning);
            Section(card.Foldout, "Panel Lock").Add(OCPasscodeEditing.Create(definition.FindPropertyRelative("_passcode")));

            var tabs = Section(card.Foldout, "Tabs & Buttons");
            tabs.Add(Field(definition, "_showLog", "Show Log", "Show the Log tab. The Log is kept from app start only when an enabled panel shows it; that is decided when the app starts."));
            tabs.Add(Field(definition, "_showPerformance", "Show Performance", "Show the Performance tab. Performance is measured only when an enabled panel shows it; that is decided when the app starts."));
            tabs.Add(Field(definition, "_saveButtonMode", "Save Button", null));

            var layout = new Foldout { text = "Layout", value = _layoutExpanded[index] };
            layout.AddToClassList("oc-panel-section");
            layout.AddToClassList("oc-panel-section--foldout");
            layout.RegisterValueChangedCallback(e =>
            {
                if (e.target == layout && index < _layoutExpanded.Count) _layoutExpanded[index] = e.newValue;
            });
            card.Foldout.Add(layout);
            AddLayoutFields(layout, definition.FindPropertyRelative("_layout"));

            _shown.Add(card);
            return root;
        }

        private static readonly (string Heading, string[] Paths)[] LayoutGroups =
        {
            ("Window", new[] { "_sizeMode", "_panelSize", "_fullScreenPadding", "_anchor", "_offset", "_backgroundColor", "_tabHeight", "_scrollbarWidth", "_scrollSensitivity" }),
            ("Rows", new[] { "_labelWidth", "_rowHeight", "_rowSpacing", "_fontSize", "_labelOverflow", "_floatDecimals", "_headerFontSize", "_headerSpacing", "_entryActionButtonWidth", "_entryActionErrorFontSize" }),
            ("Commands", new[] { "_commandDescriptionFontSize", "_commandButtonWidth" }),
            ("Log & Performance", new[] { "_logPreviewLines", "_logStackTraceFontSize", "_performanceGraphHeight", "_frameBudgetMs" }),
            ("Lock Screen", new[] { "_lockKeySize", "_lockKeySpacing", "_lockKeyFontSize", "_lockDotSize", "_lockBackgroundAlpha" }),
        };

        private static void AddLayoutFields(VisualElement parent, SerializedProperty layout)
        {
            var drawn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (heading, paths) in LayoutGroups)
            {
                Heading(parent, heading);
                foreach (var path in paths)
                {
                    var prop = layout.FindPropertyRelative(path);
                    if (prop == null) continue;
                    parent.Add(BoundField(prop, prop.displayName, null));
                    drawn.Add(path);
                }
            }

            var other = false;
            var child = layout.Copy();
            var end = layout.GetEndProperty();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                if (drawn.Contains(child.name)) continue;
                if (!other) Heading(parent, "Other");
                other = true;
                parent.Add(BoundField(child.Copy(), child.displayName, null));
            }
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("oc-panel-section");
            var header = new Label(title);
            header.AddToClassList("oc-panel-section__title");
            section.Add(header);
            parent.Add(section);
            return section;
        }

        private static void EnableWith(VisualElement owner, SerializedProperty toggle, VisualElement dependent)
        {
            dependent.SetEnabled(toggle.boolValue);
            owner.TrackPropertyValue(toggle, p => dependent.SetEnabled(p.boolValue));
        }

        private static string Summary(OCPanelDefinition definition)
        {
            var parts = new List<string>();
            if (!definition.Enabled) parts.Add("disabled");
            if (definition.HotkeyEnabled) parts.Add(definition.ToggleKey.ToString());
            if (definition.CornerTap.Enabled) parts.Add($"{CornerShort(definition.CornerTap.Corner)} corner ×{definition.CornerTap.TapCount}");
            if (definition.Passcode.IsSet) parts.Add("Locked");
            if (definition.ShowLog) parts.Add("Log");
            if (definition.ShowPerformance) parts.Add("Performance");
            return string.Join("  ·  ", parts);
        }

        private static string CornerShort(OCScreenCorner corner) => corner switch
        {
            OCScreenCorner.TopLeft => "Top-left",
            OCScreenCorner.TopRight => "Top-right",
            OCScreenCorner.BottomLeft => "Bottom-left",
            _ => "Bottom-right",
        };

        private void Remove(int index)
        {
            var panelsProp = Panels;
            var name = panelsProp.GetArrayElementAtIndex(index).FindPropertyRelative("_name").stringValue;
            if (!EditorUtility.DisplayDialog("Remove Panel Definition", $"Remove Panel Definition '{name}'? Its Passcode is lost.", "Remove", "Cancel")) return;
            panelsProp.DeleteArrayElementAtIndex(index);
            _expanded.RemoveAt(index);
            _layoutExpanded.RemoveAt(index);
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private static PropertyField Field(SerializedProperty definition, string path, string label, string? tooltip) =>
            BoundField(definition.FindPropertyRelative(path), label, tooltip);

        private static PropertyField BoundField(SerializedProperty prop, string label, string? tooltip)
        {
            var field = new PropertyField(prop, label);
            if (tooltip != null) field.tooltip = tooltip;
            field.BindProperty(prop);
            return field;
        }

        private static HelpBox Warning(VisualElement parent, string text, HelpBoxMessageType type)
        {
            var box = new HelpBox(text, type);
            box.style.display = DisplayStyle.None;
            parent.Add(box);
            return box;
        }

        private static void Heading(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("oc-subheading");
            parent.Add(label);
        }

        private static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static List<string> TriggerConflicts(OCConfig config, int index, bool lockedOnly)
        {
            var conflicts = new List<string>();
            var self = config.Panels[index];
            if (!self.Enabled || (lockedOnly && !self.Passcode.IsSet)) return conflicts;
            for (var i = 0; i < config.Panels.Count; i++)
            {
                if (i == index) continue;
                var other = config.Panels[i];
                if (!other.Enabled || (lockedOnly && !other.Passcode.IsSet)) continue;
                var sameKey = self.HotkeyEnabled && other.HotkeyEnabled && self.ToggleKey == other.ToggleKey;
                var sameCorner = self.CornerTap.Enabled && other.CornerTap.Enabled && self.CornerTap.Corner == other.CornerTap.Corner;
                if (sameKey || sameCorner) conflicts.Add($"'{other.Name}'");
            }
            return conflicts;
        }

        private static HashSet<string> DuplicateNames(SerializedProperty panelsProp)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < panelsProp.arraySize; i++)
            {
                var name = panelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue;
                if (!seen.Add(name)) duplicates.Add(name);
            }
            return duplicates;
        }

        private static string UniqueName(SerializedProperty panelsProp, string baseName)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < panelsProp.arraySize; i++)
                names.Add(panelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
            if (!names.Contains(baseName)) return baseName;
            for (var n = 2; ; n++)
                if (!names.Contains($"{baseName} {n}")) return $"{baseName} {n}";
        }

        private static SerializedProperty AddDefinition(SerializedProperty panelsProp, string name)
        {
            var index = panelsProp.arraySize;
            panelsProp.InsertArrayElementAtIndex(index);
            var definition = panelsProp.GetArrayElementAtIndex(index);
            // InsertArrayElementAtIndex copies the previous element, so every field is set here.
            definition.boxedValue = new OCPanelDefinition();
            definition.FindPropertyRelative("_name").stringValue = name;
            return definition;
        }
    }
}
