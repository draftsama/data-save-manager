#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>One Entry Definition in the Manager's Entries list: a compact row that expands to its Presentation Hints.</summary>
    internal sealed class OCEntryRow : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly Func<int, bool> _isExpanded;
        private readonly Action<int> _toggleExpanded;
        private readonly Action _structureChanged;

        private int _index = -1;
        private string _shape = string.Empty;
        private string _optionsShown = string.Empty;
        private Label? _chip;
        private Label? _key;
        private Label? _sub;
        private OCValueField? _default;
        private OCValueField? _value;
        private VisualElement? _overrideMark;
        private Button? _reset;
        private FloatField? _rangeMin;
        private FloatField? _rangeMax;
        private TextField? _headerText;
        private readonly List<(TextField Label, IntegerField Value)> _optionFields = new();

        public OCEntryRow(OCManagerContext context, Func<int, bool> isExpanded, Action<int> toggleExpanded, Action structureChanged)
        {
            _context = context;
            _isExpanded = isExpanded;
            _toggleExpanded = toggleExpanded;
            _structureChanged = structureChanged;
            AddToClassList("oc-entry");
        }

        public int Index => _index;

        public void Bind(int index)
        {
            _index = index;
            Build();
        }

        private SerializedProperty Element => _context.Entries.GetArrayElementAtIndex(_index);
        private SerializedProperty Prop(string name) => Element.FindPropertyRelative(name);
        private OCDataType Type => (OCDataType)Prop("_type").enumValueIndex;
        private bool IsValid => _index >= 0 && _index < _context.Entries.arraySize;

        private string Shape()
        {
            var exposed = Prop("_exposed").boolValue;
            return $"{Type}|{exposed}|{Prop("_hasRange").boolValue}|{Prop("_hasOptions").boolValue}|{Prop("_options").arraySize}|" +
                   $"{_isExpanded(_index)}|{_context.AudienceNames.Length}|{_context.ValuesEditable}";
        }

        private void Build()
        {
            Clear();
            _optionFields.Clear();
            _rangeMin = _rangeMax = null;
            _headerText = null;
            _optionsShown = string.Empty;
            if (!IsValid) return;

            _shape = Shape();
            var type = Type;
            var expanded = _isExpanded(_index);
            EnableInClassList("oc-entry--expanded", expanded);

            var main = new VisualElement();
            main.AddToClassList("oc-entry__main");
            Add(main);

            _chip = new Label(type.ToString());
            _chip.AddToClassList("oc-chip");
            _chip.AddToClassList($"oc-chip--{type.ToString().ToLowerInvariant()}");
            main.Add(Cell("oc-col-type", _chip));

            var keyBlock = new VisualElement { tooltip = "Click to show the Entry's settings." };
            keyBlock.AddToClassList("oc-entry__key-block");
            var caret = new Label(expanded ? "▾" : "▸");
            caret.AddToClassList("oc-caret");
            var texts = new VisualElement();
            texts.AddToClassList("oc-grow");
            _key = new Label();
            _key.AddToClassList("oc-mono");
            _sub = new Label();
            _sub.AddToClassList("oc-muted");
            var exposedMark = new VisualElement();
            exposedMark.AddToClassList("oc-exposed-mark");
            if (Prop("_exposed").boolValue)
            {
                exposedMark.tooltip = "Exposed: shown in the Runtime Panel.";
                var icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_scenevis_visible_hover" : "scenevis_visible_hover").image;
                exposedMark.style.backgroundImage = new StyleBackground((UnityEngine.Texture2D)icon);
            }
            texts.Add(_key);
            texts.Add(_sub);
            keyBlock.Add(caret);
            keyBlock.Add(exposedMark);
            keyBlock.Add(texts);
            keyBlock.RegisterCallback<ClickEvent>(_ => _toggleExpanded(_index));
            main.Add(Cell("oc-col-key", keyBlock));

            var options = UsesOptions() ? ReadOptions(Prop("_options")) : null;
            _default = new OCValueField(type, options, token =>
            {
                Prop("_defaultJson").stringValue = token.ToString(Formatting.None);
                _context.So.ApplyModifiedProperties();
            }) { tooltip = "Value used when there is no Override. Stored in the Config." };
            main.Add(Cell("oc-col-default", _default));

            var valueCell = new VisualElement();
            valueCell.AddToClassList("oc-row");
            _value = new OCValueField(type, options, token =>
            {
                if (!_context.ValuesEditable) return;
                OC.Store.SetToken(Prop("_key").stringValue, token);
                if (!EditorApplication.isPlaying) OC.Save();
            }) { tooltip = "Current value. The dot marks an Override stored in the Save File." };
            _value.AddToClassList("oc-grow");
            _value.SetEnabled(_context.ValuesEditable);
            _overrideMark = new VisualElement { tooltip = "Overridden: this value is stored in the Save File. Reset returns it to the Default." };
            _overrideMark.AddToClassList("oc-override-mark");
            valueCell.Add(_value);
            valueCell.Add(_overrideMark);
            main.Add(Cell("oc-col-value", valueCell));

            _reset = new Button(() =>
            {
                OC.Reset(Prop("_key").stringValue);
                if (!EditorApplication.isPlaying) OC.Save();
            }) { text = "Reset" };
            _reset.AddToClassList("oc-button--ghost");
            main.Add(Cell("oc-col-reset", _reset));

            if (expanded) Add(BuildDetail(type));
            Refresh();
        }

        private bool UsesOptions() =>
            OCPresentation.AllowsOptions(Type) && Prop("_hasOptions").boolValue && Prop("_options").arraySize > 0;

        private VisualElement BuildDetail(OCDataType type)
        {
            var detail = new VisualElement();
            detail.AddToClassList("oc-entry__detail");

            var identity = DetailRow();
            var key = new TextField("Key") { isDelayed = true, tooltip = "Identifier used in code: OC.Get<T>(key)." };
            key.BindProperty(Prop("_key"));
            key.AddToClassList("oc-grow");
            var typeField = new EnumField("Type", type);
            typeField.RegisterValueChangedCallback(e =>
            {
                if (e.newValue is not OCDataType newType || newType == Type) return;
                WriteHintScalars(Element, OCPresentation.ChangeType(ReadHints(Element), newType));
                _context.So.ApplyModifiedProperties();
                Build();
            });
            var remove = new Button(Remove) { text = "Remove Entry" };
            remove.AddToClassList("oc-button--danger");
            identity.Add(key);
            identity.Add(typeField);
            identity.Add(remove);
            detail.Add(identity);

            var exposedRow = DetailRow();
            var exposed = new Toggle("Exposed") { tooltip = "Shown in the Runtime Panel." };
            exposed.BindProperty(Prop("_exposed"));
            exposedRow.Add(exposed);
            var exposedOn = Prop("_exposed").boolValue;
            if (exposedOn)
            {
                var label = new TextField("Label") { tooltip = "Display name in the Runtime Panel. Falls back to the key when empty." };
                label.BindProperty(Prop("_label"));
                label.AddToClassList("oc-grow");
                exposedRow.Add(label);
            }
            detail.Add(exposedRow);
            if (!exposedOn) return detail;

            if (OCPresentation.AllowsRange(type)) detail.Add(BuildRange());
            if (OCPresentation.AllowsOptions(type)) detail.Add(BuildOptions());

            var headerRow = DetailRow();
            var hasHeader = new Toggle("Header") { tooltip = "Show a title above this Entry in the Runtime Panel." };
            hasHeader.BindProperty(Prop("_hasHeader"));
            _headerText = new TextField();
            _headerText.BindProperty(Prop("_header"));
            _headerText.AddToClassList("oc-grow");
            headerRow.Add(hasHeader);
            headerRow.Add(_headerText);
            detail.Add(headerRow);

            if (_context.AudienceNames.Length > 0)
            {
                var audienceRow = DetailRow();
                var mask = OCAudienceEditing.CreateMaskField("Audience", Prop("_audienceIds"), _context.Config, _context.AudienceNames);
                mask.tooltip = "Runtime Panels serving any of these Audiences show this Entry. None selected: every Runtime Panel shows it.";
                mask.AddToClassList("oc-grow");
                audienceRow.Add(mask);
                detail.Add(audienceRow);
            }
            return detail;
        }

        private VisualElement BuildRange()
        {
            var row = DetailRow();
            var toggle = new Toggle("Slider") { tooltip = "Show this Entry as a slider in the Runtime Panel. A presentation hint only: the Value is never clamped." };
            toggle.SetValueWithoutNotify(Prop("_hasRange").boolValue);
            toggle.RegisterValueChangedCallback(e =>
            {
                if (e.newValue && !e.previousValue) WriteHintScalars(Element, OCPresentation.TurnRangeOn(ReadHints(Element)));
                else Prop("_hasRange").boolValue = e.newValue;
                _context.So.ApplyModifiedProperties();
                Build();
            });
            row.Add(toggle);
            if (!Prop("_hasRange").boolValue) return row;

            _rangeMin = new FloatField("Min") { isDelayed = true };
            _rangeMin.BindProperty(Prop("_rangeMin"));
            _rangeMax = new FloatField("Max") { isDelayed = true };
            _rangeMax.BindProperty(Prop("_rangeMax"));
            _rangeMin.AddToClassList("oc-compact-field");
            _rangeMax.AddToClassList("oc-compact-field");
            row.Add(_rangeMin);
            row.Add(_rangeMax);
            return row;
        }

        private VisualElement BuildOptions()
        {
            var block = new VisualElement();
            var row = DetailRow();
            var toggle = new Toggle("Dropdown") { tooltip = "Show this Int Entry as a dropdown in the Runtime Panel. Each Option stores its own int. A presentation hint only: the Value is never clamped." };
            toggle.SetValueWithoutNotify(Prop("_hasOptions").boolValue);
            toggle.RegisterValueChangedCallback(e =>
            {
                if (e.newValue && !e.previousValue)
                {
                    var turnedOn = OCPresentation.TurnOptionsOn(ReadHints(Element));
                    WriteHintScalars(Element, turnedOn);
                    WriteOptions(Prop("_options"), turnedOn.Options);
                }
                else
                {
                    Prop("_hasOptions").boolValue = e.newValue;
                }
                _context.So.ApplyModifiedProperties();
                Build();
            });
            row.Add(toggle);
            block.Add(row);
            if (!Prop("_hasOptions").boolValue) return block;

            var optionsProp = Prop("_options");
            for (var i = 0; i < optionsProp.arraySize; i++)
            {
                var index = i;
                var option = optionsProp.GetArrayElementAtIndex(i);
                var optionRow = DetailRow();
                optionRow.AddToClassList("oc-option");
                var label = new TextField { tooltip = "Option label" };
                label.BindProperty(option.FindPropertyRelative("_label"));
                label.AddToClassList("oc-grow");
                var value = new IntegerField { isDelayed = true };
                value.BindProperty(option.FindPropertyRelative("_value"));
                value.AddToClassList("oc-option__value");
                var up = IconButton("▲", "Move up", () => EditOptions(p => p.MoveArrayElement(index, index - 1)));
                up.SetEnabled(i > 0);
                var down = IconButton("▼", "Move down", () => EditOptions(p => p.MoveArrayElement(index, index + 1)));
                down.SetEnabled(i < optionsProp.arraySize - 1);
                var remove = IconButton("✕", "Remove Option", () => EditOptions(p => p.DeleteArrayElementAtIndex(index)));
                optionRow.Add(label);
                optionRow.Add(value);
                optionRow.Add(up);
                optionRow.Add(down);
                optionRow.Add(remove);
                block.Add(optionRow);
                _optionFields.Add((label, value));
            }

            var addRow = DetailRow();
            addRow.AddToClassList("oc-option");
            var add = new Button(() =>
            {
                var added = OCPresentation.AddOption(ReadHints(Element));
                EditOptions(p => WriteOptions(p, added.Options));
            }) { text = "+ Add Option" };
            addRow.Add(add);
            if (optionsProp.arraySize == 0)
            {
                var empty = new Label("At least one Option is required.");
                empty.AddToClassList("oc-error-text");
                addRow.Add(empty);
            }
            block.Add(addRow);
            return block;
        }

        private void EditOptions(Action<SerializedProperty> edit)
        {
            edit(Prop("_options"));
            _context.So.ApplyModifiedProperties();
            Build();
        }

        /// <summary>Shows the latest key, values and warnings; rebuilds the row when its shape (type, hints, options count) changed.</summary>
        public void Refresh()
        {
            if (!IsValid || _key == null) return;
            if (Shape() != _shape)
            {
                Build();
                return;
            }

            var keyName = Prop("_key").stringValue;
            var label = Prop("_label").stringValue;
            var exposed = Prop("_exposed").boolValue;
            var isDuplicate = _context.DuplicateKeys.Contains(keyName);
            _key.text = keyName;
            _sub!.text = isDuplicate ? "Duplicate key" : label.Length > 0 ? label : exposed ? "Exposed" : "Not exposed";
            EnableInClassList("oc-entry--duplicate", isDuplicate);

            var viewAsIds = _context.ViewAsIds();
            var config = _context.Config;
            EnableInClassList("oc-entry--dimmed",
                viewAsIds != null && _index < config.Entries.Count && !OCAudienceFilter.Shows(config.Entries[_index], viewAsIds));

            if (UsesOptions())
            {
                var options = ReadOptions(Prop("_options"));
                var shown = string.Join("\n", options.ConvertAll(o => $"{o.Label}={o.Value}"));
                if (shown != _optionsShown)
                {
                    _optionsShown = shown;
                    _default!.SetOptions(options);
                    _value!.SetOptions(options);
                }
            }

            _default!.SetToken(OCValueField.ParseOrNull(Prop("_defaultJson").stringValue));

            var editable = _context.ValuesEditable;
            var hasOverride = editable && OC.Store.HasOverride(keyName);
            _value!.SetToken(editable ? OC.Store.GetEffectiveToken(keyName) : null);
            _overrideMark!.EnableInClassList("oc-override-mark--on", hasOverride);
            _reset!.SetEnabled(hasOverride);

            if (_rangeMin != null && _rangeMax != null)
            {
                var message = OCPresentation.Present(ReadHints(Element)).RangeMessage;
                foreach (var field in new[] { _rangeMin, _rangeMax })
                {
                    field.EnableInClassList("oc-field--error", message != null);
                    field.tooltip = message ?? string.Empty;
                }
            }

            _headerText?.SetEnabled(Prop("_hasHeader").boolValue);

            if (_optionFields.Count > 0)
            {
                var issues = OCPresentation.CheckOptions(ReadOptions(Prop("_options")));
                for (var i = 0; i < _optionFields.Count && i < issues.Length; i++)
                {
                    var (labelField, valueField) = _optionFields[i];
                    var issue = issues[i];
                    labelField.EnableInClassList("oc-field--error", (issue & OCOptionIssue.BlankLabel) != 0);
                    labelField.EnableInClassList("oc-field--warning", (issue & OCOptionIssue.DuplicateLabel) != 0);
                    labelField.tooltip = OCPresentation.DescribeLabelIssue(issue) ?? string.Empty;
                    valueField.EnableInClassList("oc-field--error", (issue & OCOptionIssue.DuplicateValue) != 0);
                    valueField.tooltip = OCPresentation.DescribeValueIssue(issue) ?? string.Empty;
                }
            }
        }

        private void Remove()
        {
            var keyName = Prop("_key").stringValue;
            if (!EditorUtility.DisplayDialog("Remove Entry", $"Remove entry '{keyName}' from the config?", "Remove", "Cancel"))
                return;

            if (EditorUtility.DisplayDialog("Reset Value", $"Also reset the current value for '{keyName}'?", "Reset", "Keep"))
            {
                OC.Reset(keyName);
                if (!EditorApplication.isPlaying) OC.Save();
            }

            _context.Entries.DeleteArrayElementAtIndex(_index);
            _context.So.ApplyModifiedProperties();
            _structureChanged();
        }

        private static VisualElement Cell(string column, VisualElement content)
        {
            var cell = new VisualElement();
            cell.AddToClassList("oc-cell");
            cell.AddToClassList(column);
            cell.Add(content);
            return cell;
        }

        private static VisualElement DetailRow()
        {
            var row = new VisualElement();
            row.AddToClassList("oc-row");
            row.AddToClassList("oc-detail-row");
            return row;
        }

        private static Button IconButton(string glyph, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { text = glyph, tooltip = tooltip };
            button.AddToClassList("oc-icon-button");
            return button;
        }

        private static List<OCOption> ReadOptions(SerializedProperty optionsProp)
        {
            var options = new List<OCOption>(optionsProp.arraySize);
            for (var i = 0; i < optionsProp.arraySize; i++)
            {
                var option = optionsProp.GetArrayElementAtIndex(i);
                options.Add(new OCOption(option.FindPropertyRelative("_label").stringValue, option.FindPropertyRelative("_value").intValue));
            }
            return options;
        }

        private static OCHintSettings ReadHints(SerializedProperty elementProp) => new()
        {
            Type = (OCDataType)elementProp.FindPropertyRelative("_type").enumValueIndex,
            DefaultJson = elementProp.FindPropertyRelative("_defaultJson").stringValue,
            HasRange = elementProp.FindPropertyRelative("_hasRange").boolValue,
            RangeMin = elementProp.FindPropertyRelative("_rangeMin").floatValue,
            RangeMax = elementProp.FindPropertyRelative("_rangeMax").floatValue,
            HasOptions = elementProp.FindPropertyRelative("_hasOptions").boolValue,
            Options = ReadOptions(elementProp.FindPropertyRelative("_options")),
            HasHeader = elementProp.FindPropertyRelative("_hasHeader").boolValue,
            Header = elementProp.FindPropertyRelative("_header").stringValue
        };

        private static void WriteHintScalars(SerializedProperty elementProp, in OCHintSettings hints)
        {
            elementProp.FindPropertyRelative("_type").enumValueIndex = (int)hints.Type;
            elementProp.FindPropertyRelative("_defaultJson").stringValue = hints.DefaultJson;
            elementProp.FindPropertyRelative("_hasRange").boolValue = hints.HasRange;
            elementProp.FindPropertyRelative("_rangeMin").floatValue = hints.RangeMin;
            elementProp.FindPropertyRelative("_rangeMax").floatValue = hints.RangeMax;
            elementProp.FindPropertyRelative("_hasOptions").boolValue = hints.HasOptions;
        }

        private static void WriteOptions(SerializedProperty optionsProp, IReadOnlyList<OCOption> options)
        {
            optionsProp.arraySize = options.Count;
            for (var i = 0; i < options.Count; i++)
            {
                var option = optionsProp.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("_label").stringValue = options[i].Label;
                option.FindPropertyRelative("_value").intValue = options[i].Value;
            }
        }
    }
}
