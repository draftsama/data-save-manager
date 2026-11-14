#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Edits a DSMConfig's entry definitions and, through <see cref="DSM"/>, the live store's values.</summary>
    internal sealed class DSMManagerWindow : EditorWindow
    {
        private static readonly Color DuplicateKeyColor = new(0.65f, 0.28f, 0.28f);
        private static readonly Color DuplicateLabelColor = new(0.75f, 0.6f, 0.2f);

        // Option add/remove/move change the card height, so they run after the list has finished drawing.
        private static Action? _deferred;

        private const float TypeWidth = 90f;
        private const float PrefixWidth = 58f;
        private const float ToggleWidth = 18f;
        private const float OverrideMarkWidth = 14f;
        private const float ResetWidth = 50f;
        private const float RemoveWidth = 24f;
        private const float CardPadding = 4f;
        private const float FieldSpacing = 4f;
        private const float InlineLabelWidth = 40f;

        private static readonly GUIContent KeyHeader = new("Key", "Identifier used in code: DSM.Get<T>(key).");
        private static readonly GUIContent TypeHeader = new("Type");
        private static readonly GUIContent LabelHeader = new("Label", "Display name in the Runtime Panel. Falls back to the key when empty.");
        private static readonly GUIContent ExposedHeader = new("Exposed", "Exposed: shown in the Runtime Panel.");
        private static readonly GUIContent DefaultHeader = new("Default", "Value used when there is no Override. Stored in the Config.");
        private static readonly GUIContent RangeHeader = new("Range", "Show this Entry as a slider in the Runtime Panel. A presentation hint only: the Value is never clamped.");
        private static readonly GUIContent RangeMinHeader = new("Min");
        private static readonly GUIContent RangeMaxHeader = new("Max");
        private static readonly GUIContent OptionsHeader = new("Options", "Show this Int Entry as a dropdown in the Runtime Panel. Each Option stores its own int. A presentation hint only: the Value is never clamped.");
        private static readonly GUIContent HeaderHeader = new("Header", "Show a title above this Entry in the Runtime Panel.");
        private static readonly GUIContent AudiencesHeader = new("Audience", "Runtime Panels serving any of these Audiences show this Entry. None selected: every Runtime Panel shows it.");
        private static readonly GUIContent ViewAsLabel = new("View as", "Grey out the Entries a Runtime Panel serving this Audience would not show.");
        private const float DimmedAlpha = 0.4f;
        private static readonly GUIContent ValueHeader = new("Value", "Current value. \u25cf marks an Override stored in the Save File.");
        private static readonly GUIContent OverrideMark = new("\u25cf", "Overridden: this value is stored in the Save File. Reset returns it to the Default.");

        private DSMConfig? _config;
        private SerializedObject? _configSo;
        private ReorderableList? _entriesList;
        private string _search = string.Empty;
        private Vector2 _entriesScroll;
        private bool _settingsFoldout = true;
        private string _newKey = string.Empty;
        private DSMDataType _newType = DSMDataType.String;
        private string[] _audienceNames = Array.Empty<string>();
        private int _viewAsIndex = -1;
        private Action? _pendingAudienceEdit;
        private bool _commandsFoldout = true;
        private DSMRegisteredCommand? _pendingCommandRun;
        private GUIStyle? _commandErrorStyle;
        private static readonly GUIContent CommandsHeader = new("Commands", "Registered Commands, listed while in Play mode. Run them here without opening the Runtime Panel.");

        [MenuItem("Draft/DSM/Open Manager")]
        internal static void Open()
        {
            var window = GetWindow<DSMManagerWindow>("DSM Manager");
            window.minSize = new Vector2(640, 480);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Reload(fromDisk: false);
        }

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        private void OnPlayModeStateChanged(PlayModeStateChange _) => Repaint();

        // Values live on DSM.Store, which changes outside this window's own GUI events (Play mode, other code) —
        // the default ~10 Hz OnInspectorUpdate tick keeps the window in sync without a bespoke poller.
        private void OnInspectorUpdate() => Repaint();

        // Opening the window must not discard unsaved in-memory values (e.g. mid Play mode); only the
        // explicit Reload button re-reads the Save File.
        private void Reload(bool fromDisk)
        {
            _config = Resources.Load<DSMConfig>("DSMConfig");
            _configSo = _config != null ? new SerializedObject(_config) : null;
            _entriesList = null; // Rebuilt lazily against the fresh SerializedObject in DrawEntries.
            if (_config != null && fromDisk) DSM.Load();
        }

        private void OnGUI()
        {
            if (_config != null) _audienceNames = DSMAudienceEditing.Names(_config);
            if (_viewAsIndex >= _audienceNames.Length) _viewAsIndex = -1;

            DrawToolbar();

            if (_config == null || _configSo == null)
            {
                DrawNoConfig();
                return;
            }

            _configSo.Update();

            var duplicates = DSMEntryRules.DuplicateKeys(_config);
            var valuesEditable = DSM.Store.Config == _config;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Config", _config, typeof(DSMConfig), false);

            if (!valuesEditable)
                EditorGUILayout.HelpBox(
                    "DSM.Store is configured with a different DSMConfig asset. This window only edits the Resources config's values, so the Value column is disabled.",
                    MessageType.Warning);

            if (duplicates.Count > 0)
                EditorGUILayout.HelpBox($"Duplicate keys: {string.Join(", ", duplicates)}", MessageType.Error);

            EditorGUILayout.Space();
            DrawSettings();

            EditorGUILayout.Space();
            DrawEntries(duplicates, valuesEditable);

            EditorGUILayout.Space();
            DrawCommands(_config);

            EditorGUILayout.Space();
            DrawAddEntry();

            EditorGUILayout.Space();
            DrawFooter();

            if (_pendingCommandRun != null)
            {
                var command = _pendingCommandRun;
                _pendingCommandRun = null;
                RunCommand(command);
            }

            if (_pendingAudienceEdit != null)
            {
                var edit = _pendingAudienceEdit;
                _pendingAudienceEdit = null;
                edit();
                Repaint();
            }

            _configSo.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    Reload(fromDisk: true);

                GUILayout.FlexibleSpace();

                if (_audienceNames.Length > 0)
                {
                    GUILayout.Label(ViewAsLabel, EditorStyles.miniLabel);
                    var choices = new string[_audienceNames.Length + 1];
                    choices[0] = "All";
                    Array.Copy(_audienceNames, 0, choices, 1, _audienceNames.Length);
                    _viewAsIndex = EditorGUILayout.Popup(_viewAsIndex + 1, choices, EditorStyles.toolbarPopup, GUILayout.Width(110)) - 1;
                }

                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }
        }

        private void DrawCommands(DSMConfig config)
        {
            _commandsFoldout = EditorGUILayout.Foldout(_commandsFoldout, CommandsHeader, true, EditorStyles.foldoutHeader);
            if (!_commandsFoldout) return;

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Commands register while the app runs. Enter Play mode to see and run them.", MessageType.None);
                return;
            }

            var registry = DSM.Commands;
            if (registry.Commands.Count == 0)
            {
                EditorGUILayout.HelpBox("No Commands are registered. An owner calls DSM.Commands.Register(this) in OnEnable.", MessageType.None);
                return;
            }

            _commandErrorStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(0.89f, 0.34f, 0.30f) } };
            var viewAsIds = _viewAsIndex >= 0 ? new[] { config.Audiences[_viewAsIndex].Id } : null;

            foreach (var command in registry.Commands)
            {
                var dimmed = viewAsIds != null && !DSMAudienceFilter.Shows(command, viewAsIds, config);
                var previousColor = GUI.color;
                if (dimmed) GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, DimmedAlpha);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(registry.DisplayTitle(command), EditorStyles.boldLabel);
                        if (command.Description != null)
                            EditorGUILayout.LabelField(command.Description, EditorStyles.wordWrappedMiniLabel);
                        if (command.Status == DSMCommandStatus.Failed && command.LastError != null)
                            EditorGUILayout.LabelField(command.LastError, _commandErrorStyle);
                    }

                    GUILayout.Label(StatusMark(command.Status), GUILayout.Width(18));
                    using (new EditorGUI.DisabledScope(command.IsRunning))
                    {
                        if (GUILayout.Button(command.IsRunning ? "Running\u2026" : "Run", GUILayout.Width(80), GUILayout.Height(22)))
                            _pendingCommandRun = command;
                    }
                }

                GUI.color = previousColor;
            }
        }

        private static string StatusMark(DSMCommandStatus status) => status switch
        {
            DSMCommandStatus.Running   => "\u25CC",
            DSMCommandStatus.Succeeded => "\u2713",
            DSMCommandStatus.Failed    => "\u2717",
            _                          => string.Empty
        };

        private static void RunCommand(DSMRegisteredCommand command)
        {
            if (command.Confirm
                && !EditorUtility.DisplayDialog("Run Command", $"Run \u201C{DSM.Commands.DisplayTitle(command)}\u201D?", "Run", "Cancel"))
                return;
            command.Run();
        }

        private void DrawNoConfig()
        {
            EditorGUILayout.HelpBox("No DSMConfig found in Resources. Create one to begin.", MessageType.Warning);
            if (GUILayout.Button("Create Config Asset", GUILayout.Height(26)))
            {
                DSMMenu.CreateConfigAsset();
                Reload(fromDisk: false);
            }
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Settings", true);
            if (!_settingsFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_configSo!.FindProperty("_autoSave"), new GUIContent("AutoSave"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_autoSaveDebounce"), new GUIContent("AutoSave Debounce"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_saveDirectory"), new GUIContent("Save Directory"));
                EditorGUILayout.PropertyField(_configSo.FindProperty("_fileName"), new GUIContent("File Name"));

                EditorGUILayout.Space(4);
                var path = DSMPaths.ResolveSaveFilePath(_config!);
                EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(18));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                            EditorUtility.RevealInFinder(dir);
                        }
                    }

                    if (GUILayout.Button("Delete Save File", GUILayout.Width(120)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Save File", $"Delete '{path}'? This cannot be undone.", "Delete", "Cancel"))
                        {
                            if (File.Exists(path)) File.Delete(path);
                            DSM.Load();
                        }
                    }
                }

                EditorGUILayout.Space(4);
                DrawAudiences();
            }
        }

        private void DrawAudiences()
        {
            var audiencesProp = _configSo!.FindProperty("_audiences");
            EditorGUILayout.LabelField(new GUIContent("Audiences", "Kinds of operator an Exposed Entry is meant for. Each Runtime Panel picks the Audiences it serves."));

            for (var i = 0; i < audiencesProp.arraySize; i++)
            {
                var audienceProp = audiencesProp.GetArrayElementAtIndex(i);
                var nameProp = audienceProp.FindPropertyRelative("_name");
                var id = audienceProp.FindPropertyRelative("_id").stringValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                    if (GUILayout.Button("✕", GUILayout.Width(RemoveWidth)))
                    {
                        var index = i;
                        var name = nameProp.stringValue;
                        _pendingAudienceEdit = () => TryRemoveAudience(audiencesProp, index, id, name);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(audiencesProp.arraySize >= DSMAudienceEditing.MaxAudiences))
            {
                if (GUILayout.Button("+ Add Audience", GUILayout.Width(120)))
                {
                    _pendingAudienceEdit = () =>
                    {
                        var index = audiencesProp.arraySize;
                        audiencesProp.InsertArrayElementAtIndex(index);
                        var added = audiencesProp.GetArrayElementAtIndex(index);
                        added.FindPropertyRelative("_id").stringValue = DSMAudience.NewId();
                        added.FindPropertyRelative("_name").stringValue = $"Audience {index + 1}";
                    };
                }
            }
        }

        private void TryRemoveAudience(SerializedProperty audiencesProp, int index, string id, string name)
        {
            // Users are found against saved data, so pending edits in this window must land first.
            _configSo!.ApplyModifiedProperties();
            var users = DSMAudienceEditing.FindUsers(_config!, id);
            if (users.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Audience in use",
                    $"'{name}' can't be removed while it is still named by:\n\n- {string.Join("\n- ", users)}\n\nRemove it from these first.",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Remove Audience", $"Remove Audience '{name}'?", "Remove", "Cancel"))
                audiencesProp.DeleteArrayElementAtIndex(index);
        }

        private void DrawEntries(IReadOnlyCollection<string> duplicates, bool valuesEditable)
        {
            var entriesProp = _configSo!.FindProperty("_entries");
            var filter = _search.Trim();
            var pendingRemoveIndex = -1;

            var list = GetOrCreateEntriesList(entriesProp);
            list.draggable = filter.Length == 0;

            bool Matches(int index)
            {
                if (filter.Length == 0) return true;
                var element = entriesProp.GetArrayElementAtIndex(index);
                var key = element.FindPropertyRelative("_key").stringValue;
                var label = element.FindPropertyRelative("_label").stringValue;
                return key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var config = _config!;
            var audienceNames = _audienceNames;
            var hasAudiences = audienceNames.Length > 0;
            var viewAsIds = _viewAsIndex >= 0 ? new[] { config.Audiences[_viewAsIndex].Id } : null;

            list.elementHeightCallback = index =>
            {
                if (!Matches(index)) return 0f;
                var element = entriesProp.GetArrayElementAtIndex(index);
                return CardHeight(ShowsRangeLine(element), OptionsLineCount(element), ShowsHeaderLine(element), ShowsAudienceLine(element, hasAudiences));
            };

            list.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                if (index < 0 || index >= entriesProp.arraySize || !Matches(index)) return;

                var type = (DSMDataType)entriesProp.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_type").enumValueIndex;
                DrawTypeTint(rect, type);
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (!Matches(index)) return;

                var element = entriesProp.GetArrayElementAtIndex(index);
                var keyProp = element.FindPropertyRelative("_key");
                var key = keyProp.stringValue;
                var isDuplicate = duplicates.Contains(key);

                var dimmed = viewAsIds != null && index < config.Entries.Count && !DSMAudienceFilter.Shows(config.Entries[index], viewAsIds);
                var priorColor = GUI.color;
                if (dimmed) GUI.color = new Color(priorColor.r, priorColor.g, priorColor.b, priorColor.a * DimmedAlpha);

                if (DrawEntryCard(rect, element, isDuplicate, valuesEditable, config, audienceNames))
                    pendingRemoveIndex = index;

                GUI.color = priorColor;
            };

            using (var scroll = new EditorGUILayout.ScrollViewScope(_entriesScroll, false, false, GUILayout.ExpandHeight(true)))
            {
                _entriesScroll = scroll.scrollPosition;
                list.DoLayoutList();
            }

            if (pendingRemoveIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(pendingRemoveIndex);

            if (_deferred != null)
            {
                var action = _deferred;
                _deferred = null;
                action();
                Repaint();
            }
        }

        private ReorderableList GetOrCreateEntriesList(SerializedProperty entriesProp)
        {
            if (_entriesList != null && _entriesList.serializedProperty.serializedObject == _configSo)
            {
                _entriesList.serializedProperty = entriesProp;
                return _entriesList;
            }

            _entriesList = new ReorderableList(_configSo, entriesProp, true, true, false, false)
            {
                headerHeight = EditorGUIUtility.singleLineHeight + 4f,
                footerHeight = 0f
            };
            _entriesList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Entries — drag ≡ to reorder (this is the Runtime Panel order)");

            return _entriesList;
        }

        private const float TypeStripeWidth = 4f;
        private const float TypeTintAlpha = 0.12f;

        private static Color TypeColor(DSMDataType type) => type switch
        {
            DSMDataType.Int => new Color(0.30f, 0.60f, 1.00f),
            DSMDataType.Float => new Color(0.25f, 0.80f, 0.85f),
            DSMDataType.Bool => new Color(1.00f, 0.60f, 0.20f),
            DSMDataType.String => new Color(0.45f, 0.85f, 0.35f),
            DSMDataType.Vector2 => new Color(0.75f, 0.45f, 1.00f),
            DSMDataType.Vector3 => new Color(1.00f, 0.40f, 0.75f),
            DSMDataType.Color => new Color(1.00f, 0.85f, 0.25f),
            _ => Color.gray
        };

        private static void DrawTypeTint(Rect rect, DSMDataType type)
        {
            var color = TypeColor(type);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, TypeTintAlpha));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, TypeStripeWidth, rect.height), color);
        }

        private static float CardHeight(bool withRangeLine, int optionsLines, bool withHeaderLine, bool withAudienceLine)
        {
            var lines = 4 + (withRangeLine ? 1 : 0) + optionsLines + (withHeaderLine ? 1 : 0) + (withAudienceLine ? 1 : 0);
            return CardPadding * 2f +
                   EditorGUIUtility.singleLineHeight * lines +
                   EditorGUIUtility.standardVerticalSpacing * (lines - 1);
        }

        private static bool ShowsRangeLine(SerializedProperty elementProp) =>
            elementProp.FindPropertyRelative("_exposed").boolValue &&
            DSMPresentation.AllowsRange((DSMDataType)elementProp.FindPropertyRelative("_type").enumValueIndex);

        private static bool ShowsOptionsToggle(SerializedProperty elementProp) =>
            elementProp.FindPropertyRelative("_exposed").boolValue &&
            DSMPresentation.AllowsOptions((DSMDataType)elementProp.FindPropertyRelative("_type").enumValueIndex);

        // Toggle line, plus (when on) one line per Option and an add line.
        private static int OptionsLineCount(SerializedProperty elementProp)
        {
            if (!ShowsOptionsToggle(elementProp)) return 0;
            if (!elementProp.FindPropertyRelative("_hasOptions").boolValue) return 1;
            return 2 + elementProp.FindPropertyRelative("_options").arraySize;
        }

        private static bool ShowsHeaderLine(SerializedProperty elementProp) =>
            elementProp.FindPropertyRelative("_exposed").boolValue;

        private static bool ShowsAudienceLine(SerializedProperty elementProp, bool hasAudiences) =>
            hasAudiences && elementProp.FindPropertyRelative("_exposed").boolValue;

        private static DSMHintSettings ReadHints(SerializedProperty elementProp)
        {
            var optionsProp = elementProp.FindPropertyRelative("_options");
            var options = new List<DSMOption>(optionsProp.arraySize);
            for (var i = 0; i < optionsProp.arraySize; i++)
            {
                var option = optionsProp.GetArrayElementAtIndex(i);
                options.Add(new DSMOption(option.FindPropertyRelative("_label").stringValue, option.FindPropertyRelative("_value").intValue));
            }

            return new DSMHintSettings
            {
                Type = (DSMDataType)elementProp.FindPropertyRelative("_type").enumValueIndex,
                DefaultJson = elementProp.FindPropertyRelative("_defaultJson").stringValue,
                HasRange = elementProp.FindPropertyRelative("_hasRange").boolValue,
                RangeMin = elementProp.FindPropertyRelative("_rangeMin").floatValue,
                RangeMax = elementProp.FindPropertyRelative("_rangeMax").floatValue,
                HasOptions = elementProp.FindPropertyRelative("_hasOptions").boolValue,
                Options = options,
                HasHeader = elementProp.FindPropertyRelative("_hasHeader").boolValue,
                Header = elementProp.FindPropertyRelative("_header").stringValue
            };
        }

        // Options are written separately (and deferred by callers) because resizing the array
        // mid-draw would outgrow the card height the list already reserved.
        private static void WriteHintScalars(SerializedProperty elementProp, in DSMHintSettings hints)
        {
            elementProp.FindPropertyRelative("_type").enumValueIndex = (int)hints.Type;
            elementProp.FindPropertyRelative("_defaultJson").stringValue = hints.DefaultJson;
            elementProp.FindPropertyRelative("_hasRange").boolValue = hints.HasRange;
            elementProp.FindPropertyRelative("_rangeMin").floatValue = hints.RangeMin;
            elementProp.FindPropertyRelative("_rangeMax").floatValue = hints.RangeMax;
            elementProp.FindPropertyRelative("_hasOptions").boolValue = hints.HasOptions;
        }

        private static void WriteOptions(SerializedProperty optionsProp, IReadOnlyList<DSMOption> options)
        {
            optionsProp.arraySize = options.Count;
            for (var i = 0; i < options.Count; i++)
            {
                var option = optionsProp.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("_label").stringValue = options[i].Label;
                option.FindPropertyRelative("_value").intValue = options[i].Value;
            }
        }

        private static void Tooltip(Rect rect, string? text)
        {
            if (text != null) GUI.Label(rect, new GUIContent(string.Empty, text));
        }

        // Every line shares one prefix column (label + override-mark slot) so all fields start at the same x.
        private static float FieldStart(Rect line) => line.x + PrefixWidth + OverrideMarkWidth;

        // Returns true when the row's remove button was clicked; the caller defers the actual
        // DeleteArrayElementAtIndex until after the list has finished drawing.
        private static bool DrawEntryCard(Rect rect, SerializedProperty elementProp, bool isDuplicate, bool valuesEditable, DSMConfig config, string[] audienceNames)
        {
            var keyProp = elementProp.FindPropertyRelative("_key");
            var typeProp = elementProp.FindPropertyRelative("_type");
            var labelProp = elementProp.FindPropertyRelative("_label");
            var exposedProp = elementProp.FindPropertyRelative("_exposed");
            var defaultJsonProp = elementProp.FindPropertyRelative("_defaultJson");
            var hasRangeProp = elementProp.FindPropertyRelative("_hasRange");
            var rangeMinProp = elementProp.FindPropertyRelative("_rangeMin");
            var rangeMaxProp = elementProp.FindPropertyRelative("_rangeMax");
            var hasHeaderProp = elementProp.FindPropertyRelative("_hasHeader");
            var headerProp = elementProp.FindPropertyRelative("_header");
            var hasOptionsProp = elementProp.FindPropertyRelative("_hasOptions");
            var optionsProp = elementProp.FindPropertyRelative("_options");
            var key = keyProp.stringValue;
            var type = (DSMDataType)typeProp.enumValueIndex;
            // Decided before drawing so it matches the height the list already reserved for this card.
            var showRange = ShowsRangeLine(elementProp);
            var showOptions = ShowsOptionsToggle(elementProp);
            var showHeader = ShowsHeaderLine(elementProp);
            var showAudiences = ShowsAudienceLine(elementProp, audienceNames.Length > 0);

            var lineH = EditorGUIUtility.singleLineHeight;
            var vSpace = EditorGUIUtility.standardVerticalSpacing;
            var y = rect.y + CardPadding;
            Rect NextLine()
            {
                var line = new Rect(rect.x, y, rect.width, lineH);
                y = line.yMax + vSpace;
                return line;
            }

            var priorLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = InlineLabelWidth;

            var removeClicked = DrawKeyLine(NextLine(), elementProp, keyProp, isDuplicate, ref type);
            DrawExposedLine(NextLine(), exposedProp, labelProp);
            DrawDefaultField(NextLine(), defaultJsonProp, type, hasOptionsProp, optionsProp);
            DrawValueLine(NextLine(), key, type, valuesEditable);
            if (showRange && DSMPresentation.AllowsRange(type))
                DrawRangeLine(NextLine(), elementProp, hasRangeProp, rangeMinProp, rangeMaxProp, type);
            if (showOptions)
                DrawOptionsLines(NextLine, elementProp, hasOptionsProp, optionsProp);
            if (showHeader)
                DrawHeaderLine(NextLine(), hasHeaderProp, headerProp);
            if (showAudiences)
                DrawAudienceLine(NextLine(), elementProp.FindPropertyRelative("_audienceIds"), config, audienceNames);

            EditorGUIUtility.labelWidth = priorLabelWidth;
            return removeClicked && TryRemoveEntry(key);
        }

        private static bool DrawKeyLine(
            Rect rect,
            SerializedProperty elementProp,
            SerializedProperty keyProp,
            bool isDuplicate,
            ref DSMDataType type)
        {
            var removeRect = new Rect(rect.xMax - RemoveWidth, rect.y, RemoveWidth, rect.height);
            var typeRect = new Rect(removeRect.x - FieldSpacing - TypeWidth, rect.y, TypeWidth, rect.height);
            var fieldX = FieldStart(rect);
            var keyRect = new Rect(fieldX, rect.y, Mathf.Max(60f, typeRect.x - FieldSpacing - fieldX), rect.height);

            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), KeyHeader);
            var priorColor = GUI.backgroundColor;
            if (isDuplicate) GUI.backgroundColor = DuplicateKeyColor;
            keyProp.stringValue = EditorGUI.TextField(keyRect, keyProp.stringValue);
            GUI.backgroundColor = priorColor;

            var newType = (DSMDataType)EditorGUI.EnumPopup(typeRect, TypeHeader, type);
            if (newType != type)
            {
                WriteHintScalars(elementProp, DSMPresentation.ChangeType(ReadHints(elementProp), newType));
                type = newType;
            }

            return GUI.Button(removeRect, "✕");
        }

        private static void DrawExposedLine(Rect rect, SerializedProperty exposedProp, SerializedProperty labelProp)
        {
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), ExposedHeader);
            var toggleRect = new Rect(fieldX, rect.y, ToggleWidth, rect.height);
            exposedProp.boolValue = EditorGUI.Toggle(toggleRect, exposedProp.boolValue);

            if (!exposedProp.boolValue) return;

            var labelX = toggleRect.xMax + FieldSpacing;
            var labelRect = new Rect(labelX, rect.y, rect.xMax - RemoveWidth - FieldSpacing - labelX, rect.height);
            labelProp.stringValue = EditorGUI.TextField(labelRect, LabelHeader, labelProp.stringValue);
        }

        private static void DrawRangeLine(
            Rect rect,
            SerializedProperty elementProp,
            SerializedProperty hasRangeProp,
            SerializedProperty minProp,
            SerializedProperty maxProp,
            DSMDataType type)
        {
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), RangeHeader);

            var toggleRect = new Rect(fieldX, rect.y, ToggleWidth, rect.height);
            var wasOn = hasRangeProp.boolValue;
            var isOn = EditorGUI.Toggle(toggleRect, wasOn);
            if (isOn && !wasOn) WriteHintScalars(elementProp, DSMPresentation.TurnRangeOn(ReadHints(elementProp)));
            else hasRangeProp.boolValue = isOn;

            if (!isOn) return;

            var startX = toggleRect.xMax + FieldSpacing;
            var fieldWidth = (rect.xMax - RemoveWidth - FieldSpacing - startX - FieldSpacing) * 0.5f;
            var minRect = new Rect(startX, rect.y, fieldWidth, rect.height);
            var maxRect = new Rect(minRect.xMax + FieldSpacing, rect.y, fieldWidth, rect.height);

            var rangeMessage = DSMPresentation.Present(ReadHints(elementProp)).RangeMessage;
            var priorColor = GUI.backgroundColor;
            if (rangeMessage != null) GUI.backgroundColor = DuplicateKeyColor;

            if (type == DSMDataType.Int)
            {
                minProp.floatValue = EditorGUI.IntField(minRect, RangeMinHeader, Mathf.RoundToInt(minProp.floatValue));
                maxProp.floatValue = EditorGUI.IntField(maxRect, RangeMaxHeader, Mathf.RoundToInt(maxProp.floatValue));
            }
            else
            {
                minProp.floatValue = EditorGUI.FloatField(minRect, RangeMinHeader, minProp.floatValue);
                maxProp.floatValue = EditorGUI.FloatField(maxRect, RangeMaxHeader, maxProp.floatValue);
            }

            GUI.backgroundColor = priorColor;
            Tooltip(minRect, rangeMessage);
            Tooltip(maxRect, rangeMessage);
        }

        private static void DrawHeaderLine(Rect rect, SerializedProperty hasHeaderProp, SerializedProperty headerProp)
        {
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), HeaderHeader);

            var toggleRect = new Rect(fieldX, rect.y, ToggleWidth, rect.height);
            hasHeaderProp.boolValue = EditorGUI.Toggle(toggleRect, hasHeaderProp.boolValue);

            var textX = toggleRect.xMax + FieldSpacing;
            var textRect = new Rect(textX, rect.y, rect.xMax - RemoveWidth - FieldSpacing - textX, rect.height);
            using (new EditorGUI.DisabledScope(!hasHeaderProp.boolValue))
                headerProp.stringValue = EditorGUI.TextField(textRect, headerProp.stringValue);
        }

        private static void DrawAudienceLine(Rect rect, SerializedProperty audienceIdsProp, DSMConfig config, string[] audienceNames)
        {
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), AudiencesHeader);
            var fieldRect = new Rect(fieldX, rect.y, rect.xMax - RemoveWidth - FieldSpacing - fieldX, rect.height);
            DSMAudienceEditing.DrawMaskField(fieldRect, GUIContent.none, audienceIdsProp, config, audienceNames);
        }

        private static void DrawOptionsLines(
            Func<Rect> nextLine,
            SerializedProperty elementProp,
            SerializedProperty hasOptionsProp,
            SerializedProperty optionsProp)
        {
            var rect = nextLine();
            var fieldX = FieldStart(rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), OptionsHeader);

            var toggleRect = new Rect(fieldX, rect.y, ToggleWidth, rect.height);
            var wasOn = hasOptionsProp.boolValue;
            var isOn = EditorGUI.Toggle(toggleRect, wasOn);

            if (isOn && !wasOn)
            {
                var before = ReadHints(elementProp);
                var turnedOn = DSMPresentation.TurnOptionsOn(before);
                WriteHintScalars(elementProp, turnedOn);
                if (turnedOn.Options.Count != before.Options.Count)
                    _deferred = () => WriteOptions(optionsProp, turnedOn.Options);
            }
            else
            {
                hasOptionsProp.boolValue = isOn;
            }

            if (!isOn) return;

            var hints = ReadHints(elementProp);
            var issues = DSMPresentation.CheckOptions(hints.Options);

            const float valueWidth = 80f;
            const float buttonWidth = 22f;

            for (var i = 0; i < optionsProp.arraySize; i++)
            {
                var row = nextLine();
                var option = optionsProp.GetArrayElementAtIndex(i);
                var labelProp = option.FindPropertyRelative("_label");
                var valueProp = option.FindPropertyRelative("_value");

                var removeRect = new Rect(row.xMax - RemoveWidth, row.y, RemoveWidth, row.height);
                var downRect = new Rect(removeRect.x - FieldSpacing - buttonWidth, row.y, buttonWidth, row.height);
                var upRect = new Rect(downRect.x - buttonWidth, row.y, buttonWidth, row.height);
                var valueRect = new Rect(upRect.x - FieldSpacing - valueWidth, row.y, valueWidth, row.height);
                var labelRect = new Rect(fieldX, row.y, Mathf.Max(40f, valueRect.x - FieldSpacing - fieldX), row.height);

                var issue = issues[i];
                var priorColor = GUI.backgroundColor;
                if ((issue & DSMOptionIssue.BlankLabel) != 0) GUI.backgroundColor = DuplicateKeyColor;
                else if ((issue & DSMOptionIssue.DuplicateLabel) != 0) GUI.backgroundColor = DuplicateLabelColor;
                labelProp.stringValue = EditorGUI.TextField(labelRect, labelProp.stringValue);

                GUI.backgroundColor = (issue & DSMOptionIssue.DuplicateValue) != 0 ? DuplicateKeyColor : priorColor;
                valueProp.intValue = EditorGUI.IntField(valueRect, GUIContent.none, valueProp.intValue);
                GUI.backgroundColor = priorColor;
                Tooltip(labelRect, DSMPresentation.DescribeLabelIssue(issue));
                Tooltip(valueRect, DSMPresentation.DescribeValueIssue(issue));

                var index = i;
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUI.Button(upRect, "▲")) _deferred = () => optionsProp.MoveArrayElement(index, index - 1);
                using (new EditorGUI.DisabledScope(i == optionsProp.arraySize - 1))
                    if (GUI.Button(downRect, "▼")) _deferred = () => optionsProp.MoveArrayElement(index, index + 1);
                if (GUI.Button(removeRect, "✕")) _deferred = () => optionsProp.DeleteArrayElementAtIndex(index);
            }

            var addRow = nextLine();
            var addRect = new Rect(fieldX, addRow.y, 110f, addRow.height);
            if (GUI.Button(addRect, "+ Add Option"))
            {
                var added = DSMPresentation.AddOption(hints);
                _deferred = () => WriteOptions(optionsProp, added.Options);
            }

            if (optionsProp.arraySize == 0)
                EditorGUI.LabelField(new Rect(addRect.xMax + FieldSpacing, addRow.y, addRow.xMax - addRect.xMax - FieldSpacing - RemoveWidth, addRow.height),
                    "At least one Option is required.", EditorStyles.miniLabel);
        }

        private static void DrawDefaultField(
            Rect rect,
            SerializedProperty defaultJsonProp,
            DSMDataType type,
            SerializedProperty hasOptionsProp,
            SerializedProperty optionsProp)
        {
            var fieldX = FieldStart(rect);
            var fieldRect = new Rect(fieldX, rect.y, rect.xMax - ResetWidth - FieldSpacing - fieldX, rect.height);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, PrefixWidth, rect.height), DefaultHeader);

            if (DSMPresentation.AllowsOptions(type) && hasOptionsProp.boolValue && optionsProp.arraySize > 0)
            {
                DrawOptionDefaultPopup(fieldRect, defaultJsonProp, optionsProp);
                return;
            }

            JToken? defaultToken;
            try { defaultToken = JToken.Parse(defaultJsonProp.stringValue); }
            catch { defaultToken = null; }

            var newDefaultToken = DSMValueField.DrawField(fieldRect, GUIContent.none, type, defaultToken, out var changed);
            if (changed)
                defaultJsonProp.stringValue = newDefaultToken.ToString(Formatting.None);
        }

        private static void DrawOptionDefaultPopup(Rect fieldRect, SerializedProperty defaultJsonProp, SerializedProperty optionsProp)
        {
            var options = new DSMOption[optionsProp.arraySize];
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var option = optionsProp.GetArrayElementAtIndex(i);
                options[i] = new DSMOption(option.FindPropertyRelative("_label").stringValue, option.FindPropertyRelative("_value").intValue);
                labels[i] = $"{options[i].Label} ({options[i].Value})";
            }

            var selected = int.TryParse(defaultJsonProp.stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var current)
                ? DSMPresentation.IndexOfOption(options, current)
                : -1;

            var next = EditorGUI.Popup(fieldRect, selected, labels);
            if (next >= 0 && next != selected)
                defaultJsonProp.stringValue = options[next].Value.ToString(CultureInfo.InvariantCulture);
        }

        private static void DrawValueLine(Rect rect, string key, DSMDataType type, bool valuesEditable)
        {
            var hasOverride = valuesEditable && DSM.Store.HasOverride(key);

            var resetRect = new Rect(rect.xMax - ResetWidth, rect.y, ResetWidth, rect.height);
            var labelRect = new Rect(rect.x, rect.y, PrefixWidth, rect.height);
            var markRect = new Rect(labelRect.xMax, rect.y, OverrideMarkWidth, rect.height);
            var fieldX = FieldStart(rect);
            var fieldRect = new Rect(fieldX, rect.y, resetRect.x - FieldSpacing - fieldX, rect.height);

            EditorGUI.LabelField(labelRect, ValueHeader);
            GUI.Label(markRect, hasOverride ? OverrideMark : GUIContent.none, hasOverride ? EditorStyles.boldLabel : EditorStyles.label);

            using (new EditorGUI.DisabledScope(!valuesEditable))
            {
                var valueToken = valuesEditable ? DSM.Store.GetEffectiveToken(key) : null;
                var newValueToken = DSMValueField.DrawField(fieldRect, GUIContent.none, type, valueToken, out var changed);
                if (valuesEditable && changed)
                {
                    DSM.Store.SetToken(key, newValueToken);
                    if (!EditorApplication.isPlaying) DSM.Save();
                }
            }

            using (new EditorGUI.DisabledScope(!valuesEditable || !DSM.Store.HasOverride(key)))
            {
                if (GUI.Button(resetRect, "Reset"))
                {
                    DSM.Reset(key);
                    if (!EditorApplication.isPlaying) DSM.Save();
                }
            }
        }

        // Returns true when the user confirmed removal; the caller still owns deleting the
        // array element, deferred until after the list has finished drawing.
        private static bool TryRemoveEntry(string key)
        {
            if (!EditorUtility.DisplayDialog("Remove Entry", $"Remove entry '{key}' from the config?", "Remove", "Cancel"))
                return false;

            if (EditorUtility.DisplayDialog("Reset Value", $"Also reset the current value for '{key}'?", "Reset", "Keep"))
            {
                DSM.Reset(key);
                if (!EditorApplication.isPlaying) DSM.Save();
            }

            return true;
        }

        private void DrawAddEntry()
        {
            EditorGUILayout.LabelField("Add Entry", EditorStyles.boldLabel);

            var error = DSMEntryRules.ValidateNewKey(_config!, _newKey);

            using (new EditorGUILayout.HorizontalScope())
            {
                _newKey = EditorGUILayout.TextField(_newKey, GUILayout.Width(150));
                _newType = (DSMDataType)EditorGUILayout.EnumPopup(_newType, GUILayout.Width(90));

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        AddEntry(_newKey.Trim(), _newType);
                        _newKey = string.Empty;
                    }
                }
            }

            if (_newKey.Length > 0 && error != null)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void AddEntry(string key, DSMDataType type)
        {
            var entriesProp = _configSo!.FindProperty("_entries");
            var index = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(index);

            var element = entriesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("_key").stringValue = key;
            element.FindPropertyRelative("_type").enumValueIndex = (int)type;
            element.FindPropertyRelative("_label").stringValue = string.Empty;
            element.FindPropertyRelative("_exposed").boolValue = false;
            element.FindPropertyRelative("_defaultJson").stringValue = DSMEntryDefinition.DefaultJsonFor(type);
            element.FindPropertyRelative("_hasRange").boolValue = false;
            element.FindPropertyRelative("_rangeMin").floatValue = 0f;
            element.FindPropertyRelative("_rangeMax").floatValue = 0f;
            element.FindPropertyRelative("_hasHeader").boolValue = false;
            element.FindPropertyRelative("_header").stringValue = string.Empty;
            element.FindPropertyRelative("_hasOptions").boolValue = false;
            element.FindPropertyRelative("_options").ClearArray();
            element.FindPropertyRelative("_audienceIds").ClearArray();
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset All Values", GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog("Reset All Values", "Reset all overrides to their defaults?", "Reset", "Cancel"))
                    {
                        DSM.ResetAll();
                        if (!EditorApplication.isPlaying) DSM.Save();
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Overrides: {DSM.Store.OverrideKeys.Count}", EditorStyles.miniLabel);
            }
        }
    }
}
