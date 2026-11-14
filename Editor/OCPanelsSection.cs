#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Runtime Panels tab: edits the Config's Panel Definitions.</summary>
    internal sealed class OCPanelsSection : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly ScrollView _scroll = new();
        private readonly VisualElement _cards = new();
        private readonly List<bool> _expanded = new();
        private readonly List<Card> _shown = new();
        private string _shownAudiences = string.Empty;
        private int _shownCount = -1;

        private sealed class Card
        {
            public Foldout Foldout = null!;
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

            var actions = new VisualElement();
            actions.AddToClassList("oc-row");
            actions.AddToClassList("oc-actions");
            var add = new Button(() =>
            {
                var panelsProp = Panels;
                AddDefinition(panelsProp, UniqueName(panelsProp, "Panel"));
                _context.So.ApplyModifiedProperties();
                Refresh();
            }) { text = "+ Add Panel" };
            add.AddToClassList("oc-button--primary");
            var import = new Button(() =>
            {
                var imported = ImportScenePanels(Panels);
                _context.So.ApplyModifiedProperties();
                Refresh();
                ShowImportResult(imported);
            }) { text = "Import Scene Panels", tooltip = "Create a Panel Definition from each Runtime Panel placed in the open scenes." };
            actions.Add(add);
            actions.Add(import);
            _scroll.Add(actions);
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
                card.Foldout.text = enabled ? name : $"{name} (disabled)";
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

            var content = card.Foldout;
            content.Add(Field(definition, "_name", "Name", "Code finds this Runtime Panel by this name: OC.Panels.Get(name)."));
            card.DuplicateName = Warning(content, "Another Panel Definition has this name; OC.Panels.Get returns only the first.", HelpBoxMessageType.Error);
            content.Add(Field(definition, "_enabled", "Enabled", "Disabled Panel Definitions create no Runtime Panel."));
            content.Add(Field(definition, "_sortOrder", "Sort Order", null));

            if (_context.Config.Audiences.Count > 0)
            {
                var mask = OCAudienceEditing.CreateMaskField("Audiences", definition.FindPropertyRelative("_audienceIds"), _context.Config, _context.AudienceNames);
                mask.tooltip = "Exposed Entries this Panel shows: those naming any of these Audiences, plus those naming none. None selected: every Exposed Entry.";
                content.Add(mask);
                card.NoAudience = Warning(content, "This Panel serves no Audience, so it shows every Exposed Entry, including ones meant for other Audiences.", HelpBoxMessageType.Warning);
            }

            Heading(content, "Open Triggers");
            content.Add(Field(definition, "_hotkeyEnabled", "Hotkey Enabled", null));
            content.Add(Field(definition, "_toggleKey", "Toggle Key", null));
            card.KeyWarning = Warning(content, "Not a keyboard key: the hotkey does nothing with the Input System.", HelpBoxMessageType.Error);
            content.Add(Field(definition, "_cornerTap", "Corner Tap", null));
            card.Conflicts = Warning(content, string.Empty, HelpBoxMessageType.Warning);
            card.LockedConflicts = Warning(content, string.Empty, HelpBoxMessageType.Warning);

            Heading(content, "Panel Lock");
            content.Add(OCPasscodeEditing.Create(definition.FindPropertyRelative("_passcode")));

            Heading(content, "Behaviour");
            content.Add(Field(definition, "_startVisible", "Start Visible", null));
            content.Add(Field(definition, "_saveButtonMode", "Save Button Mode", null));
            content.Add(Field(definition, "_layout", "Layout", null));

            _shown.Add(card);
            return root;
        }

        private void Remove(int index)
        {
            var panelsProp = Panels;
            var name = panelsProp.GetArrayElementAtIndex(index).FindPropertyRelative("_name").stringValue;
            if (!EditorUtility.DisplayDialog("Remove Panel Definition", $"Remove Panel Definition '{name}'? Its Passcode is lost.", "Remove", "Cancel")) return;
            panelsProp.DeleteArrayElementAtIndex(index);
            _expanded.RemoveAt(index);
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private static PropertyField Field(SerializedProperty definition, string path, string label, string? tooltip)
        {
            var prop = definition.FindPropertyRelative(path);
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

        /// <summary>Appends one Panel Definition per Runtime Panel placed in the open scenes; returns a line per panel imported. The scenes are not changed.</summary>
        internal static List<string> ImportScenePanels(SerializedProperty panelsProp)
        {
            var panels = Object.FindObjectsByType<OCRuntimePanel>(FindObjectsInactive.Include);
            var imported = new List<string>();
            foreach (var panel in panels)
            {
                if (!panel.gameObject.scene.IsValid()) continue;
                var definition = AddDefinition(panelsProp, UniqueName(panelsProp, panel.gameObject.name));

                using var panelSo = new SerializedObject(panel);
                Copy(panelSo, "_legacyAudienceIds", definition, "_audienceIds");
                Copy(panelSo, "_legacySaveButtonMode", definition, "_saveButtonMode");
                Copy(panelSo, "_legacyHotkeyEnabled", definition, "_hotkeyEnabled");
                Copy(panelSo, "_legacyToggleKey", definition, "_toggleKey");
                Copy(panelSo, "_legacyCornerTap", definition, "_cornerTap");
                Copy(panelSo, "_legacyPasscode", definition, "_passcode");
                Copy(panelSo, "_legacyStartVisible", definition, "_startVisible");
                Copy(panelSo, "_legacyLayout", definition, "_layout");
                if (panel.TryGetComponent<Canvas>(out var canvas))
                    definition.FindPropertyRelative("_sortOrder").intValue = canvas.sortingOrder;
                imported.Add($"'{panel.gameObject.name}' in {panel.gameObject.scene.name}");
            }
            return imported;
        }

        private static void ShowImportResult(List<string> imported)
        {
            EditorUtility.DisplayDialog(
                "Import Scene Panels",
                imported.Count == 0
                    ? "No Runtime Panel is placed in the open scenes."
                    : $"Created a Panel Definition from:\n\n- {string.Join("\n- ", imported)}\n\nRemove these panels from their scenes and save the scenes; until then they stay hidden in Play mode.",
                "OK");
        }

        private static void Copy(SerializedObject from, string fromPath, SerializedProperty to, string toPath)
        {
            var source = from.FindProperty(fromPath);
            var target = to.FindPropertyRelative(toPath);
            if (source == null || target == null) return;
            if (!source.isArray)
            {
                target.boxedValue = source.boxedValue;
                return;
            }
            // boxedValue can't read arrays, so their elements are copied one by one.
            target.arraySize = source.arraySize;
            for (var i = 0; i < source.arraySize; i++)
                target.GetArrayElementAtIndex(i).boxedValue = source.GetArrayElementAtIndex(i).boxedValue;
        }
    }
}
