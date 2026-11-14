#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>Edits an OCConfig's entry definitions and, through <see cref="OC"/>, the live store's values.</summary>
    internal sealed class OCManagerWindow : EditorWindow
    {
        private const long RefreshIntervalMs = 150;
        private enum Tab { Entries, Panels, Commands, Settings }

        [SerializeField] private Tab _tab;

        private OCConfig? _config;
        private SerializedObject? _configSo;
        private OCManagerContext? _context;
        private readonly Dictionary<Tab, Button> _navButtons = new();
        private readonly Dictionary<Tab, Label> _navCounts = new();
        private VisualElement? _content;
        private VisualElement? _filterBar;
        private ToolbarSearchField? _search;
        private PopupField<string>? _viewAs;
        private HelpBox? _storeMismatch;
        private Label? _modeLabel;
        private VisualElement? _modeDot;
        private OCEntriesTab? _entriesTab;
        private OCPanelsSection? _panelsTab;
        private OCCommandsTab? _commandsTab;
        private OCSettingsTab? _settingsTab;
        private string _shownAudiences = string.Empty;

        [MenuItem("Draft/Operator Console/Open Manager")]
        internal static void Open()
        {
            var window = GetWindow<OCManagerWindow>("Operator Console");
            window.minSize = new Vector2(720, 480);
            window.Show();
        }

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo() => Rebuild();

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var sheet = LoadStyleSheet();
            if (sheet != null) root.styleSheets.Add(sheet);
            root.AddToClassList("oc-root");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "oc-dark" : "oc-light");
            Load(fromDisk: false);
            root.schedule.Execute(Tick).Every(RefreshIntervalMs);
        }

        // Opening the window must not discard unsaved in-memory values (e.g. mid Play mode); only the
        // Reload button re-reads the Save File.
        private void Load(bool fromDisk)
        {
            _config = Resources.Load<OCConfig>("DSMConfig");
            _configSo = _config != null ? new SerializedObject(_config) : null;
            if (_config != null && fromDisk) OC.Load();
            Rebuild();
        }

        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            _navButtons.Clear();
            _navCounts.Clear();
            _entriesTab = null;
            _panelsTab = null;
            _commandsTab = null;
            _settingsTab = null;

            root.Add(BuildToolbar());

            if (_config == null || _configSo == null)
            {
                _context = null;
                root.Add(BuildNoConfig());
                return;
            }

            _configSo.Update();
            _context = new OCManagerContext(_config, _configSo) { AudienceNames = OCAudienceEditing.Names(_config) };
            _shownAudiences = string.Join("\n", _context.AudienceNames);

            var body = new VisualElement();
            body.AddToClassList("oc-body");
            body.Add(BuildNav());

            var main = new VisualElement();
            main.AddToClassList("oc-main");
            _filterBar = BuildFilterBar();
            main.Add(_filterBar);
            _storeMismatch = new HelpBox(
                "OC.Store is configured with a different OCConfig asset. This window only edits the Resources config's values, so the Value column is disabled.",
                HelpBoxMessageType.Warning);
            _storeMismatch.AddToClassList("oc-banner");
            main.Add(_storeMismatch);
            _content = new VisualElement();
            _content.AddToClassList("oc-content");
            main.Add(_content);
            body.Add(main);
            root.Add(body);

            ShowTab(_tab);
        }

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("oc-toolbar");

            var title = new Label("Operator Console");
            title.AddToClassList("oc-toolbar__title");
            bar.Add(title);

            if (_config != null)
            {
                var config = _config;
                var pill = new Button(() =>
                {
                    EditorGUIUtility.PingObject(config);
                    Selection.activeObject = config;
                }) { text = config.name, tooltip = "Select the Config asset" };
                pill.AddToClassList("oc-pill");
                bar.Add(pill);
            }

            var spacer = new VisualElement();
            spacer.AddToClassList("oc-grow");
            bar.Add(spacer);

            var mode = new VisualElement();
            mode.AddToClassList("oc-row");
            mode.AddToClassList("oc-mode");
            _modeDot = new VisualElement();
            _modeDot.AddToClassList("oc-mode__dot");
            _modeLabel = new Label();
            mode.Add(_modeDot);
            mode.Add(_modeLabel);
            bar.Add(mode);

            var reload = new Button(() => Load(fromDisk: true)) { text = "Reload", tooltip = "Re-read the Config and the Save File." };
            bar.Add(reload);
            UpdateMode();
            return bar;
        }

        private VisualElement BuildNav()
        {
            var nav = new VisualElement();
            nav.AddToClassList("oc-nav");
            AddNavItem(nav, Tab.Entries, "Entries", "The Config's Entry Definitions and their current values.");
            AddNavItem(nav, Tab.Panels, "Runtime Panels", "Each enabled Panel Definition becomes one Runtime Panel when the app starts.");
            AddNavItem(nav, Tab.Commands, "Commands", "Registered Commands, listed while in Play mode. Run them here without opening the Runtime Panel.");
            AddNavItem(nav, Tab.Settings, "Settings", "Save File location and behaviour, and the Config's Audiences.");
            return nav;
        }

        private void AddNavItem(VisualElement nav, Tab tab, string text, string tooltip)
        {
            var button = new Button(() => ShowTab(tab)) { tooltip = tooltip };
            button.AddToClassList("oc-nav__item");
            var label = new Label(text);
            label.AddToClassList("oc-grow");
            var count = new Label();
            count.AddToClassList("oc-nav__count");
            button.Add(label);
            button.Add(count);
            nav.Add(button);
            _navButtons[tab] = button;
            _navCounts[tab] = count;
        }

        private VisualElement BuildFilterBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("oc-filter-bar");

            _search = new ToolbarSearchField { tooltip = "Search Entries by key or label" };
            _search.AddToClassList("oc-search");
            _search.SetValueWithoutNotify(_context!.Search);
            _search.RegisterValueChangedCallback(e =>
            {
                _context!.Search = e.newValue;
                _entriesTab?.Rebuild();
            });
            bar.Add(_search);

            var choices = new List<string> { "All audiences" };
            choices.AddRange(_context.AudienceNames);
            if (_context.ViewAsIndex >= _context.AudienceNames.Length) _context.ViewAsIndex = -1;
            _viewAs = new PopupField<string>("View as", choices, _context.ViewAsIndex + 1)
            {
                tooltip = "Grey out the Entries and Commands a Runtime Panel serving this Audience would not show."
            };
            _viewAs.AddToClassList("oc-view-as");
            _viewAs.RegisterValueChangedCallback(_ => _context!.ViewAsIndex = _viewAs.index - 1);
            _viewAs.style.display = _context.AudienceNames.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            bar.Add(_viewAs);
            return bar;
        }

        private VisualElement BuildNoConfig()
        {
            var empty = new VisualElement();
            empty.AddToClassList("oc-empty");
            var title = new Label("No DSMConfig in Resources");
            title.AddToClassList("oc-empty__title");
            var text = new Label("Create a Config asset to start adding Entries and Runtime Panels.");
            text.AddToClassList("oc-muted");
            var create = new Button(() =>
            {
                OCMenu.CreateConfigAsset();
                Load(fromDisk: false);
            }) { text = "Create Config Asset" };
            create.AddToClassList("oc-button--primary");
            empty.Add(title);
            empty.Add(text);
            empty.Add(create);
            return empty;
        }

        private void ShowTab(Tab tab)
        {
            _tab = tab;
            if (_context == null || _content == null) return;

            foreach (var (key, button) in _navButtons)
                button.EnableInClassList("oc-nav__item--selected", key == tab);
            var hasAudiences = _context.AudienceNames.Length > 0;
            if (_filterBar != null)
                _filterBar.style.display = tab == Tab.Entries || (tab == Tab.Commands && hasAudiences) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_search != null)
                _search.style.display = tab == Tab.Entries ? DisplayStyle.Flex : DisplayStyle.None;

            _content.Clear();
            _entriesTab = null;
            _panelsTab = null;
            _commandsTab = null;
            _settingsTab = null;
            switch (tab)
            {
                case Tab.Entries: _content.Add(_entriesTab = new OCEntriesTab(_context)); break;
                case Tab.Panels: _content.Add(_panelsTab = new OCPanelsSection(_context)); break;
                case Tab.Commands: _content.Add(_commandsTab = new OCCommandsTab(_context)); break;
                case Tab.Settings: _content.Add(_settingsTab = new OCSettingsTab(_context)); break;
            }
            Tick();
        }

        // Values live on OC.Store, which changes outside this window (Play mode, other code), so the window polls it.
        private void Tick()
        {
            UpdateMode();
            if (_context == null)
            {
                if (Resources.Load<OCConfig>("DSMConfig") != null) Load(fromDisk: false);
                return;
            }

            if (_config == null)
            {
                Load(fromDisk: false);
                return;
            }

            _context.So.UpdateIfRequiredOrScript();
            var audiences = OCAudienceEditing.Names(_config);
            if (string.Join("\n", audiences) != _shownAudiences)
            {
                Rebuild();
                return;
            }

            _context.DuplicateKeys = OCEntryRules.DuplicateKeys(_config);
            if (_storeMismatch != null)
                _storeMismatch.style.display = _context.ValuesEditable ? DisplayStyle.None : DisplayStyle.Flex;

            _navCounts[Tab.Entries].text = _context.DuplicateKeys.Count > 0 ? $"⚠ {_config.Entries.Count}" : _config.Entries.Count.ToString();
            _navCounts[Tab.Panels].text = _config.Panels.Count.ToString();
            _navCounts[Tab.Commands].text = EditorApplication.isPlaying ? OC.Commands.Commands.Count.ToString() : string.Empty;

            _entriesTab?.Refresh();
            _panelsTab?.Refresh();
            _commandsTab?.Refresh();
            _settingsTab?.Refresh();
        }

        private void UpdateMode()
        {
            if (_modeLabel == null || _modeDot == null) return;
            var playing = EditorApplication.isPlaying;
            _modeLabel.text = playing ? "Live · Play mode" : "Edit mode";
            _modeDot.EnableInClassList("oc-mode__dot--live", playing);
        }

        private StyleSheet? LoadStyleSheet()
        {
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            if (string.IsNullOrEmpty(scriptPath)) return null;
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(scriptPath[..^".cs".Length] + ".uss");
        }
    }
}
