#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OperatorConsole
{
    public enum OCSaveButtonMode
    {
        Auto,
        Always,
        Never
    }

    public enum OCPanelTab
    {
        Settings,
        Commands,
        Log,
        Performance
    }

    /// <summary>The in-game UI where an operator views and edits Exposed Entries and runs Commands and Entry Actions while the app runs. Created from a Panel Definition; see <see cref="OC.Panels"/>.</summary>
    public sealed class OCRuntimePanel : MonoBehaviour
    {
        private const string ThemePath = "OperatorConsole/OCRuntimeTheme";
        private const string ResetConfirmMessage = "Reset all settings on this panel to their defaults?\nThis cannot be undone.";
        private const string ResetConfirmButtonText = "Reset";
        private const float TitleScale = 1.2f;
        private const float PillScale = 0.65f;
        private const float TabInset = 8f;
        private const float FooterButtonExtra = 6f;
        private static readonly Vector2Int ReferenceResolution = new(1920, 1080);

        private static ThemeStyleSheet? s_theme;

        private OCPanelDefinition? _definition;
        private const string EditingFieldClass = "ocp-field--editing";

        private UIDocument? _document;
        private VisualElement? _editingField;
        private PanelSettings? _panelSettings;
        private bool _countedOpen;
        private bool _visible;
        private static int s_openCount;

        private VisualElement? _card;
        private Label? _title;
        private Label? _pill;
        private VisualElement? _tabBar;
        private Button? _settingsTab;
        private Button? _commandsTab;
        private Button? _logTab;
        private Label? _logBadge;
        private Button? _performanceTab;
        private ScrollView? _settingsView;
        private ScrollView? _commandsView;
        private OCLogView? _logView;
        private OCPerformanceView? _performanceView;
        private Button? _saveButton;
        private Button? _resetButton;
        private Button? _closeButton;
        private VisualElement? _confirmDialog;
        private Label? _confirmMessage;
        private Button? _confirmCancelButton;
        private Button? _confirmButton;
        private OCLockScreen? _lockScreen;

        private readonly List<OCRegisteredCommand> _shownCommands = new();
        private readonly List<IOCLayoutAware> _entryLayoutTargets = new();
        private readonly List<IOCLayoutAware> _commandLayoutTargets = new();
        private int _shownEntryCount;
        private OCPanelTab _tab;
        private OCPanelTab? _rememberedTab;
        private long _errorsSeen;
        private int _shownUnseenErrors = -1;
        private Action? _pendingConfirm;
        private Action? _onUnlocked;
        private Action? _onLockCancelled;

#if ENABLE_INPUT_SYSTEM
        private KeyCode _parsedToggleKey = KeyCode.None;
        private Key _inputSystemToggleKey;
        private bool _hasInputSystemToggleKey;
#endif

        public static event Action<bool>? OpenChanged;

        /// <summary>True while any Runtime Panel, or its Lock Screen, is open.</summary>
        public static bool IsOpen => s_openCount > 0;

        /// <summary>The name of the Panel Definition this Runtime Panel was created from.</summary>
        public string Name => Definition.Name;

        public OCPanelLayout Layout => Definition.Layout;

        /// <summary>Ids of the Audiences this Panel serves; empty means it shows every Exposed Entry.</summary>
        public IReadOnlyList<string> AudienceIds => Definition.AudienceIds;

        private OCPanelDefinition Definition => _definition!;

        public bool IsVisible => _visible;

        /// <summary>True while the Lock Screen is asking for this Panel's Passcode.</summary>
        public bool IsLocking => _lockScreen != null && _lockScreen.IsShown;

        public bool HasPanelLock => Definition.Passcode.IsSet;

        private static OCStore Store => OC.Store;

        private static void SaveStore() => Store.Save();

        /// <summary>Gives the panel its Panel Definition and a UI Toolkit document. Call on an inactive GameObject; false, after logging an error, when the runtime theme is missing.</summary>
        internal bool Initialize(OCPanelDefinition definition)
        {
            _definition = definition;
            if (s_theme == null) s_theme = Resources.Load<ThemeStyleSheet>(ThemePath);
            if (s_theme == null)
            {
                Debug.LogError($"OC: no theme at Resources/{ThemePath}.tss, so no Runtime Panel was created. Reimport the Operator Console package.");
                return false;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = $"OC Panel Settings ({definition.Name})";
            _panelSettings.themeStyleSheet = s_theme;
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = ReferenceResolution;
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = definition.SortOrder;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            return true;
        }

        private void Awake()
        {
            if (_definition != null) return;
            Debug.LogWarning($"OCRuntimePanel on '{gameObject.name}' was placed in a scene. Runtime Panels are now created from Panel Definitions: add one in the Operator Console, then remove this panel from the scene.", this);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_definition != null) OC.Commands.Changed += OnCommandsChanged;
        }

        private void OnDisable()
        {
            if (_definition != null) OC.Commands.Changed -= OnCommandsChanged;
        }

        // UIDocument creates its root in its own OnEnable, so the tree is built here or on first use, whichever comes first.
        private void Start()
        {
            if (_definition == null || !EnsureBuilt()) return;
            if (Definition.StartVisible && !_visible) Show();
        }

        private void Update()
        {
            if (_definition == null) return;
            _lockScreen?.Tick();

            if (Definition.HotkeyEnabled && _editingField == null && HotkeyPressed()) Toggle();

            var cornerTap = Definition.CornerTap;
            if (cornerTap.Enabled && TryGetPointerPress(out var position))
            {
                if (IsVisible || IsLocking) cornerTap.ResetCount();
                else if (cornerTap.RegisterPress(position, new Vector2(Screen.width, Screen.height), Time.unscaledTimeAsDouble)) RequestOpen();
            }

            if (!_visible) return;
            if (_tab == OCPanelTab.Log)
            {
                _logView!.Refresh();
                MarkLogSeen();
            }
            else if (_tab == OCPanelTab.Performance)
            {
                _performanceView!.Refresh();
            }
            UpdateLogBadge();
        }

        private bool HotkeyPressed()
        {
            var toggleKey = Definition.ToggleKey;
#if ENABLE_INPUT_SYSTEM
            // Parsed only when the key changes, since the Operator Console can change it in Play mode.
            if (toggleKey != _parsedToggleKey)
            {
                _parsedToggleKey = toggleKey;
                _hasInputSystemToggleKey = OCKeyMap.TryGetKey(toggleKey, out _inputSystemToggleKey);
                if (!_hasInputSystemToggleKey)
                    Debug.LogWarning($"OCRuntimePanel '{Name}': Toggle Key {toggleKey} is not a keyboard key, so the hotkey does nothing. Pick another key in the Operator Console.", this);
            }
            return _hasInputSystemToggleKey && Keyboard.current != null && Keyboard.current[_inputSystemToggleKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleKey);
#else
            return false;
#endif
        }

        private static bool TryGetPointerPress(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }

        /// <summary>Opens the panel the way an Open Trigger does: through the Lock Screen when a Passcode is set.</summary>
        public void RequestOpen()
        {
            if (IsVisible || IsLocking || !EnsureBuilt()) return;
            var passcode = Definition.Passcode;
            if (!passcode.IsSet)
            {
                Show();
                return;
            }
            if (!OC.Panels.ClaimLockScreen(this)) return;
            _lockScreen!.ApplyLayout(Layout);
            _lockScreen.Open(passcode, _onUnlocked!, _onLockCancelled!);
            SetOpen(true);
        }

        private void OnLockCancelled()
        {
            OC.Panels.ReleaseLockScreen(this);
            SetOpen(false);
        }

        /// <summary>Closes the Lock Screen without opening the panel, because another panel's Lock Screen took the turn.</summary>
        internal void DropLockScreen()
        {
            if (!IsLocking) return;
            _lockScreen!.Close();
            SetOpen(false);
        }

        /// <summary>Opens the panel at once, without asking for the Passcode.</summary>
        public void Show()
        {
            if (!EnsureBuilt()) return;
            OC.Panels.ReleaseLockScreen(this);
            if (IsLocking) _lockScreen!.Close();
            CloseConfirmDialog();
            Rebuild();
            RebuildCommands();
            _tab = _rememberedTab is { } remembered && IsAvailable(remembered) ? remembered : DefaultTab();
            ApplyTabs();
            _card!.style.display = DisplayStyle.Flex;
            _visible = true;
            SetOpen(true);
        }

        public void Hide()
        {
            OC.Panels.ReleaseLockScreen(this);
            if (IsLocking) _lockScreen!.Close();
            CloseConfirmDialog();
            // A delayed field commits its edit on blur, so blur first or Auto Save misses it.
            _document?.rootVisualElement?.focusController?.focusedElement?.Blur();
            if (Store.Config.AutoSave && Store.IsDirty) Store.Save();
            if (_card != null) _card.style.display = DisplayStyle.None;
            if (_visible) _rememberedTab = _tab;
            _visible = false;
            SetOpen(false);
        }

        public void SelectSettings() => SelectTab(OCPanelTab.Settings);

        public void SelectCommands() => SelectTab(OCPanelTab.Commands);

        public void SelectLog() => SelectTab(OCPanelTab.Log);

        public void SelectPerformance() => SelectTab(OCPanelTab.Performance);

        /// <summary>The tab shown while the panel is open.</summary>
        public OCPanelTab SelectedTab => _tab;

        /// <summary>Shows <paramref name="tab"/>; does nothing when this panel doesn't have that tab.</summary>
        public void SelectTab(OCPanelTab tab)
        {
            if (!IsAvailable(tab)) return;
            CloseConfirmDialog();
            _tab = tab;
            ApplyTabs();
        }

        private bool IsAvailable(OCPanelTab tab) => tab switch
        {
            OCPanelTab.Commands    => _shownCommands.Count > 0,
            OCPanelTab.Log         => Definition.ShowLog,
            OCPanelTab.Performance => Definition.ShowPerformance,
            _                       => true
        };

        private OCPanelTab DefaultTab()
        {
            if (_shownEntryCount > 0) return OCPanelTab.Settings;
            foreach (var tab in new[] { OCPanelTab.Commands, OCPanelTab.Log, OCPanelTab.Performance })
                if (IsAvailable(tab)) return tab;
            return OCPanelTab.Settings;
        }

        private void ApplyTabs()
        {
            if (_card == null) return;
            if (!IsAvailable(_tab)) _tab = OCPanelTab.Settings;

            var hasCommands = IsAvailable(OCPanelTab.Commands);
            var hasLog = IsAvailable(OCPanelTab.Log);
            var hasPerformance = IsAvailable(OCPanelTab.Performance);
            SetShown(_tabBar, hasCommands || hasLog || hasPerformance);
            SetShown(_commandsTab, hasCommands);
            SetShown(_logTab, hasLog);
            SetShown(_performanceTab, hasPerformance);

            _settingsTab!.EnableInClassList("ocp-tab--selected", _tab == OCPanelTab.Settings);
            _commandsTab!.EnableInClassList("ocp-tab--selected", _tab == OCPanelTab.Commands);
            _logTab!.EnableInClassList("ocp-tab--selected", _tab == OCPanelTab.Log);
            _performanceTab!.EnableInClassList("ocp-tab--selected", _tab == OCPanelTab.Performance);
            SetShown(_settingsView, _tab == OCPanelTab.Settings);
            SetShown(_commandsView, _tab == OCPanelTab.Commands);
            SetShown(_logView, _tab == OCPanelTab.Log);
            SetShown(_performanceView, _tab == OCPanelTab.Performance);
            SetShown(_resetButton, _tab == OCPanelTab.Settings);
            ApplySaveButtonMode();

            if (_tab == OCPanelTab.Log)
            {
                _logView!.Refresh(true);
                MarkLogSeen();
            }
            UpdateLogBadge();
        }

        private void MarkLogSeen() => _errorsSeen = OC.Log.ErrorsLogged;

        private void UpdateLogBadge()
        {
            if (_logBadge == null) return;
            var unseen = Definition.ShowLog ? OC.Log.UnseenErrors(_errorsSeen) : 0;
            if (unseen == _shownUnseenErrors) return;
            _shownUnseenErrors = unseen;
            _logBadge.text = unseen > 99 ? "99+" : unseen.ToString();
            SetShown(_logBadge, unseen > 0);
        }

        private void OnCommandsChanged()
        {
            if (!IsVisible) return;
            Rebuild();
            RebuildCommands();
            ApplyTabs();
        }

        private void OnResetClicked() => OpenConfirmDialog(ResetConfirmMessage, ResetConfirmButtonText, ResetShownEntries);

        private void ResetShownEntries() => Store.ResetExposedEntries(Definition.AudienceIds);

        private void OnRunClicked(OCRegisteredCommand command)
        {
            if (!command.Confirm) command.Run();
            else OpenConfirmDialog($"Run “{OC.Commands.DisplayTitle(command)}”?", "Run", command.Run);
        }

        private void OnEntryActionClicked(OCRegisteredEntryAction action)
        {
            void Run() => action.Run(Store);

            if (!action.Confirm) Run();
            else OpenConfirmDialog($"Run “{OC.Commands.DisplayTitle(action)}”?", "Run", Run);
        }

        private void OpenConfirmDialog(string message, string buttonText, Action onConfirm)
        {
            if (_confirmDialog == null) return;
            _confirmMessage!.text = message;
            _confirmButton!.text = buttonText;
            _pendingConfirm = onConfirm;
            _confirmDialog.style.display = DisplayStyle.Flex;
        }

        private void OnConfirmClicked()
        {
            var onConfirm = _pendingConfirm;
            CloseConfirmDialog();
            onConfirm?.Invoke();
        }

        private void CloseConfirmDialog()
        {
            _pendingConfirm = null;
            SetShown(_confirmDialog, false);
        }

        private void ApplySaveButtonMode()
        {
            if (_saveButton == null) return;

            var active = Definition.SaveButtonMode switch
            {
                OCSaveButtonMode.Always => true,
                OCSaveButtonMode.Never  => false,
                _                        => !Store.Config.AutoSave
            };
            SetShown(_saveButton, active && _tab == OCPanelTab.Settings);
        }

        private void OnDestroy()
        {
            SetOpen(false);
            if (_definition != null) OC.Panels.Remove(this);
            // Detached first: the UIDocument may still be live when this runs.
            if (_document != null) _document.panelSettings = null;
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void SetOpen(bool open)
        {
            if (_countedOpen == open) return;
            _countedOpen = open;
            var wasOpen = IsOpen;
            s_openCount += open ? 1 : -1;
            if (IsOpen != wasOpen) OpenChanged?.Invoke(IsOpen);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount() => s_openCount = 0;

        /// <summary>Closes the panel or its Lock Screen, or opens it through <see cref="RequestOpen"/>.</summary>
        public void Toggle()
        {
            if (IsVisible) Hide();
            else if (IsLocking) _lockScreen!.Cancel();
            else RequestOpen();
        }

        /// <summary>Applies the Panel Definition's layout and Save Button Mode again, for edits made while the panel is up.</summary>
        public void ApplySettings()
        {
            if (_definition == null || _card == null) return;

            if (_panelSettings != null) _panelSettings.sortingOrder = Definition.SortOrder;
            ApplyPanelLayout();
            ApplySaveButtonMode();
            foreach (var target in _entryLayoutTargets) target.ApplyLayout(Layout);
            foreach (var target in _commandLayoutTargets) target.ApplyLayout(Layout);
            ApplyRowSpacing(_settingsView);
            ApplyRowSpacing(_commandsView);
            _logView!.ApplyLayout(Layout);
            _performanceView!.ApplyLayout(Layout);
            ApplyTabs();
        }

        public void Rebuild()
        {
            if (!EnsureBuilt()) return;
            ApplyPanelLayout();
            _shownEntryCount = 0;
            _entryLayoutTargets.Clear();
            var container = _settingsView!.contentContainer;
            container.Clear();

            var store = Store;
            var registry = OC.Commands;
            foreach (var entry in store.Config.Entries)
            {
                if (!OCAudienceFilter.Shows(entry, Definition.AudienceIds)) continue;
                _shownEntryCount++;

                var presentation = OCPresentation.Present(OCHintSettings.From(entry));

                if (presentation.RangeMessage != null)
                    Debug.LogWarning($"OCRuntimePanel on '{gameObject.name}': entry '{entry.Key}': {presentation.RangeMessage}", this);

                if (presentation.OptionsMessage != null)
                    Debug.LogWarning($"OCRuntimePanel on '{gameObject.name}': entry '{entry.Key}': {presentation.OptionsMessage}", this);

                var widget = OCWidgets.Create(presentation.Widget, entry.Type);
                if (widget == null)
                {
                    Debug.LogError($"OCRuntimePanel on '{gameObject.name}': no widget for '{entry.Key}' ({entry.Type}).", this);
                    continue;
                }

                if (presentation.Header != null) AddLayoutTarget(container, new OCHeaderRow(presentation.Header), _entryLayoutTargets);

                AddLayoutTarget(container, widget, _entryLayoutTargets);
                widget.Setup(entry, store);

                var actions = registry.EntryActionsFor(entry.Key);
                if (actions.Count == 0) continue;
                var error = new OCEntryActionError();
                container.Add(error);
                _entryLayoutTargets.Add(new OCEntryActionBar(widget, widget.Field, actions, registry.DisplayTitle, OnEntryActionClicked, error));
            }

            foreach (var target in _entryLayoutTargets) target.ApplyLayout(Layout);
            ApplyRowSpacing(_settingsView);
        }

        /// <summary>Lists the Registered Commands this Panel shows on its Commands tab.</summary>
        public void RebuildCommands()
        {
            _shownCommands.Clear();
            var registry = OC.Commands;
            var config = Store.Config;
            foreach (var command in registry.Commands)
                if (OCAudienceFilter.Shows(command, Definition.AudienceIds, config)) _shownCommands.Add(command);

            if (!EnsureBuilt()) return;
            _commandLayoutTargets.Clear();
            var container = _commandsView!.contentContainer;
            container.Clear();

            string? previousHeader = null;
            foreach (var command in _shownCommands)
            {
                var startsGroup = command.Header != null && !string.Equals(command.Header, previousHeader, StringComparison.Ordinal);
                previousHeader = command.Header;
                if (startsGroup) AddLayoutTarget(container, new OCHeaderRow(command.Header!), _commandLayoutTargets);
                AddLayoutTarget(container, new OCCommandRow(command, registry.DisplayTitle(command), OnRunClicked), _commandLayoutTargets);
            }

            foreach (var target in _commandLayoutTargets) target.ApplyLayout(Layout);
            ApplyRowSpacing(_commandsView);
        }

        private static void AddLayoutTarget<TElement>(VisualElement container, TElement element, List<IOCLayoutAware> targets)
            where TElement : VisualElement, IOCLayoutAware
        {
            container.Add(element);
            targets.Add(element);
        }

        private void ApplyRowSpacing(ScrollView? view)
        {
            if (view == null) return;
            foreach (var child in view.contentContainer.Children())
                child.style.marginBottom = Layout.RowSpacing;
        }

        private void ApplyPanelLayout()
        {
            if (_card == null) return;
            var layout = Layout;
            var s = _card.style;
            _card.EnableInClassList("ocp--edge", layout.SizeMode == OCPanelSizeMode.FullScreen && layout.FullScreenPadding <= 0f);

            if (layout.SizeMode == OCPanelSizeMode.FullScreen)
            {
                var padding = layout.FullScreenPadding;
                s.left = padding;
                s.top = padding;
                s.right = padding;
                s.bottom = padding;
                s.width = StyleKeyword.Auto;
                s.height = StyleKeyword.Auto;
                s.translate = new Translate(0f, 0f);
                s.marginLeft = 0f;
                s.marginTop = 0f;
            }
            else
            {
                // uGUI anchors count y from the bottom; UI Toolkit counts it from the top.
                var point = AnchorPoint(layout.Anchor);
                var x = point.x * 100f;
                var y = (1f - point.y) * 100f;
                s.left = Length.Percent(x);
                s.top = Length.Percent(y);
                s.right = StyleKeyword.Auto;
                s.bottom = StyleKeyword.Auto;
                s.width = layout.PanelSize.x;
                s.height = layout.PanelSize.y;
                s.translate = new Translate(Length.Percent(-x), Length.Percent(-y));
                s.marginLeft = layout.Offset.x;
                s.marginTop = -layout.Offset.y;
            }
            s.backgroundColor = layout.BackgroundColor;

            _title!.style.fontSize = layout.FontSize * TitleScale;
            _pill!.style.fontSize = layout.FontSize * PillScale;
            foreach (var tab in new[] { _settingsTab!, _commandsTab!, _logTab!, _performanceTab! })
            {
                tab.style.height = layout.TabHeight - TabInset;
                tab.style.fontSize = layout.FontSize;
            }
            _logBadge!.style.fontSize = layout.FontSize * PillScale;
            foreach (var view in new[] { _settingsView!, _commandsView! })
            {
                view.verticalScroller.style.width = layout.ScrollbarWidth;
                view.mouseWheelScrollSize = layout.ScrollSensitivity;
            }
            foreach (var button in new[] { _saveButton!, _resetButton!, _closeButton!, _confirmCancelButton!, _confirmButton! })
            {
                button.style.fontSize = layout.FontSize;
                button.style.height = layout.RowHeight + FooterButtonExtra;
            }
            _confirmMessage!.style.fontSize = layout.FontSize;
            _lockScreen!.ApplyLayout(layout);
        }

        private static Vector2 AnchorPoint(OCPanelAnchor anchor) => anchor switch
        {
            OCPanelAnchor.Left        => new Vector2(0f, 0.5f),
            OCPanelAnchor.Right       => new Vector2(1f, 0.5f),
            OCPanelAnchor.Top         => new Vector2(0.5f, 1f),
            OCPanelAnchor.Bottom      => new Vector2(0.5f, 0f),
            OCPanelAnchor.TopLeft     => new Vector2(0f, 1f),
            OCPanelAnchor.TopRight    => new Vector2(1f, 1f),
            OCPanelAnchor.BottomLeft  => new Vector2(0f, 0f),
            OCPanelAnchor.BottomRight => new Vector2(1f, 0f),
            _                          => new Vector2(0.5f, 0.5f)
        };

        private static void SetShown(VisualElement? element, bool shown)
        {
            if (element != null) element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnFocusIn(FocusInEvent e)
        {
            var field = TextFieldOf(e.target as VisualElement);
            if (field == _editingField) return;
            _editingField?.RemoveFromClassList(EditingFieldClass);
            _editingField = field;
            _editingField?.AddToClassList(EditingFieldClass);
        }

        private void OnFocusOut(FocusOutEvent e)
        {
            if (_editingField == null || TextFieldOf(e.relatedTarget as VisualElement) == _editingField) return;
            _editingField.RemoveFromClassList(EditingFieldClass);
            _editingField = null;
        }

        private static VisualElement? TextFieldOf(VisualElement? element)
        {
            for (var e = element; e != null; e = e.parent)
                if (e.ClassListContains(TextInputBaseField<string>.ussClassName)) return e;
            return null;
        }

        private bool EnsureBuilt()
        {
            if (_card != null) return true;
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return false;

            root.pickingMode = PickingMode.Ignore;
            var overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("ocp-overlay");

            _card = new VisualElement();
            _card.AddToClassList("ocp");
            _card.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _card.RegisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);

            var head = new VisualElement();
            head.AddToClassList("ocp-head");
            _title = new Label("Settings");
            _title.AddToClassList("ocp-title");
            _pill = new Label(Definition.Name);
            _pill.AddToClassList("ocp-pill");
            head.Add(_title);
            head.Add(_pill);
            _card.Add(head);

            _tabBar = new VisualElement();
            _tabBar.AddToClassList("ocp-tabs");
            _settingsTab = TabButton("Settings", SelectSettings);
            _commandsTab = TabButton("Commands", SelectCommands);
            _logTab = TabButton(string.Empty, SelectLog);
            _logTab.AddToClassList("ocp-tab--with-badge");
            _logTab.Add(new Label("Log"));
            _logBadge = new Label();
            _logBadge.AddToClassList("ocp-tab__badge");
            _logTab.Add(_logBadge);
            _performanceTab = TabButton("Performance", SelectPerformance);
            _tabBar.Add(_settingsTab);
            _tabBar.Add(_commandsTab);
            _tabBar.Add(_logTab);
            _tabBar.Add(_performanceTab);
            _card.Add(_tabBar);

            _settingsView = ScrollView();
            _commandsView = ScrollView();
            _logView = new OCLogView(OC.Log);
            _performanceView = new OCPerformanceView(OC.Performance);
            _card.Add(_settingsView);
            _card.Add(_commandsView);
            _card.Add(_logView);
            _card.Add(_performanceView);

            var footer = new VisualElement();
            footer.AddToClassList("ocp-foot");
            _resetButton = FooterButton("Reset to Defaults", "ocp-button--danger", OnResetClicked);
            var spacer = new VisualElement();
            spacer.AddToClassList("ocp-grow");
            _closeButton = FooterButton("Close", "ocp-button--ghost", Hide);
            _saveButton = FooterButton("Save", "ocp-button--primary", SaveStore);
            footer.Add(_resetButton);
            footer.Add(spacer);
            footer.Add(_closeButton);
            footer.Add(_saveButton);
            _card.Add(footer);

            _confirmDialog = new VisualElement();
            _confirmDialog.AddToClassList("ocp-confirm");
            var box = new VisualElement();
            box.AddToClassList("ocp-confirm__box");
            _confirmMessage = new Label();
            _confirmMessage.AddToClassList("ocp-confirm__message");
            var buttons = new VisualElement();
            buttons.AddToClassList("ocp-confirm__buttons");
            _confirmCancelButton = FooterButton("Cancel", "ocp-button--ghost", CloseConfirmDialog);
            _confirmButton = FooterButton(ResetConfirmButtonText, "ocp-button--danger", OnConfirmClicked);
            buttons.Add(_confirmCancelButton);
            buttons.Add(_confirmButton);
            box.Add(_confirmMessage);
            box.Add(buttons);
            _confirmDialog.Add(box);
            _card.Add(_confirmDialog);

            overlay.Add(_card);
            _lockScreen = new OCLockScreen();
            overlay.Add(_lockScreen);
            root.Add(overlay);

            _onUnlocked = Show;
            _onLockCancelled = OnLockCancelled;
            _card.style.display = DisplayStyle.None;
            CloseConfirmDialog();
            SetShown(_tabBar, false);
            SetShown(_commandsView, false);
            SetShown(_logView, false);
            SetShown(_performanceView, false);
            SetShown(_logBadge, false);
            ApplyPanelLayout();
            _logView.ApplyLayout(Layout);
            _performanceView.ApplyLayout(Layout);
            ApplySaveButtonMode();
            return true;
        }

        private static Button TabButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ocp-tab");
            return button;
        }

        private static Button FooterButton(string text, string variant, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ocp-button");
            button.AddToClassList(variant);
            return button;
        }

        private static ScrollView ScrollView()
        {
            var view = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            view.AddToClassList("ocp-scroll");
            return view;
        }
    }
}
