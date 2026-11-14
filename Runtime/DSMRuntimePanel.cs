#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DataSaveManager
{
    public enum DSMSaveButtonMode
    {
        Auto,
        Always,
        Never
    }

    /// <summary>The in-game UI where an operator views and edits Exposed Entries and runs Commands and Entry Actions while the app runs. Created from a Panel Definition; see <see cref="DSM.Panels"/>.</summary>
    public sealed class DSMRuntimePanel : MonoBehaviour
    {
        [SerializeField] private DSMWidgetConfig? _widgetConfig;
        [SerializeField] private GameObject? _root;
        [SerializeField] private Transform? _container;
        [SerializeField] private Button? _saveButton;
        [SerializeField, FormerlySerializedAs("_resetAllButton")] private Button? _resetButton;
        [SerializeField] private Button? _closeButton;
        [SerializeField] private GameObject? _confirmDialog;
        [SerializeField] private Button? _confirmCancelButton;
        [SerializeField] private Button? _confirmResetButton;
        [SerializeField, Tooltip("Keypad shown before the panel opens when a Passcode is set.")]
        private DSMLockScreen? _lockScreen;
        [SerializeField] private GameObject? _tabBar;
        [SerializeField] private DSMPanelTab? _settingsTab;
        [SerializeField] private DSMPanelTab? _commandsTab;
        [SerializeField] private GameObject? _settingsView;
        [SerializeField] private GameObject? _commandsView;
        [SerializeField] private Transform? _commandContainer;
        [SerializeField] private TMP_Text? _confirmMessage;
        [SerializeField] private TMP_Text? _confirmButtonLabel;

        // Settings from before Panel Definitions; read only by the DSM Manager's import from scene panels.
#pragma warning disable CS0414
        [SerializeField, HideInInspector, FormerlySerializedAs("_saveButtonMode")] private DSMSaveButtonMode _legacySaveButtonMode = DSMSaveButtonMode.Auto;
        [SerializeField, HideInInspector, FormerlySerializedAs("_hotkeyEnabled")] private bool _legacyHotkeyEnabled = true;
        [SerializeField, HideInInspector, FormerlySerializedAs("_toggleKey")] private KeyCode _legacyToggleKey = KeyCode.F1;
        [SerializeField, HideInInspector, FormerlySerializedAs("_cornerTap")] private DSMCornerTap _legacyCornerTap = new();
        [SerializeField, HideInInspector, FormerlySerializedAs("_passcode")] private DSMPasscode _legacyPasscode = new();
        [SerializeField, HideInInspector, FormerlySerializedAs("_startVisible")] private bool _legacyStartVisible;
        [SerializeField, HideInInspector, FormerlySerializedAs("_layout")] private DSMPanelLayout _legacyLayout = new();
        [SerializeField, HideInInspector, FormerlySerializedAs("_audienceIds")] private List<string> _legacyAudienceIds = new();
#pragma warning restore CS0414

        private DSMPanelDefinition? _definition;
        private bool _countedOpen;
        private static int s_openCount;

        private readonly List<DSMRegisteredCommand> _shownCommands = new();
        private int _shownEntryCount;
        private bool _commandsSelected;
        private Action? _pendingConfirm;
        private string _resetConfirmMessage = string.Empty;
        private string _resetConfirmButtonText = string.Empty;
        private bool _warnedMissingCommandRow;
        private bool _warnedMissingEntryActionBar;
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

        public DSMPanelLayout Layout => Definition.Layout;

        /// <summary>Ids of the Audiences this Panel serves; empty means it shows every Exposed Entry.</summary>
        public IReadOnlyList<string> AudienceIds => Definition.AudienceIds;

        private DSMPanelDefinition Definition => _definition!;

        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>True while the Lock Screen is asking for this Panel's Passcode.</summary>
        public bool IsLocking => _lockScreen != null && _lockScreen.IsShown;

        public bool HasPanelLock => Definition.Passcode.IsSet;

        private static DSMStore Store => DSM.Store;

        private static void SaveStore() => Store.Save();

        internal void Initialize(DSMPanelDefinition definition)
        {
            _definition = definition;
            if (TryGetComponent<Canvas>(out var canvas)) canvas.sortingOrder = definition.SortOrder;
        }

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}' was placed in a scene. Runtime Panels are now created from Panel Definitions: import this one in the DSM Manager, then remove it from the scene.", this);
                gameObject.SetActive(false);
                return;
            }

            if (_saveButton != null) _saveButton.onClick.AddListener(SaveStore);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_confirmCancelButton != null) _confirmCancelButton.onClick.AddListener(CloseConfirmDialog);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(OnConfirmClicked);
            if (_settingsTab != null && _settingsTab.Button != null) _settingsTab.Button.onClick.AddListener(SelectSettings);
            if (_commandsTab != null && _commandsTab.Button != null) _commandsTab.Button.onClick.AddListener(SelectCommands);
            if (_confirmMessage != null) _resetConfirmMessage = _confirmMessage.text;
            if (_confirmButtonLabel != null) _resetConfirmButtonText = _confirmButtonLabel.text;
            _onUnlocked = Show;
            _onLockCancelled = OnLockCancelled;
            if (_lockScreen != null) _lockScreen.Close();

            CloseConfirmDialog();
            ApplySaveButtonMode();

            if (Definition.StartVisible) Show();
            else if (_root != null) _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_definition != null) DSM.Commands.Changed += OnCommandsChanged;
        }

        private void OnDisable()
        {
            if (_definition != null) DSM.Commands.Changed -= OnCommandsChanged;
        }

        private void Update()
        {
            if (Definition.HotkeyEnabled && HotkeyPressed()) Toggle();

            var cornerTap = Definition.CornerTap;
            if (cornerTap.Enabled && TryGetPointerPress(out var position))
            {
                if (IsVisible || IsLocking) cornerTap.ResetCount();
                else if (cornerTap.RegisterPress(position, new Vector2(Screen.width, Screen.height), Time.unscaledTimeAsDouble)) RequestOpen();
            }
        }

        private bool HotkeyPressed()
        {
            var toggleKey = Definition.ToggleKey;
#if ENABLE_INPUT_SYSTEM
            // Parsed only when the key changes, since the DSM Manager can change it in Play mode.
            if (toggleKey != _parsedToggleKey)
            {
                _parsedToggleKey = toggleKey;
                _hasInputSystemToggleKey = DSMKeyMap.TryGetKey(toggleKey, out _inputSystemToggleKey);
                if (!_hasInputSystemToggleKey)
                    Debug.LogWarning($"DSMRuntimePanel '{Name}': Toggle Key {toggleKey} is not a keyboard key, so the hotkey does nothing. Pick another key in the DSM Manager.", this);
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
            if (IsVisible || IsLocking) return;
            var passcode = Definition.Passcode;
            if (!passcode.IsSet)
            {
                Show();
                return;
            }
            if (_lockScreen == null)
            {
                Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': a Passcode is set but no Lock Screen is assigned, so the panel stays closed.", this);
                return;
            }
            if (!DSM.Panels.ClaimLockScreen(this)) return;
            _lockScreen.ApplyLayout(Layout);
            _lockScreen.Open(passcode, _onUnlocked!, _onLockCancelled!);
            SetOpen(true);
        }

        private void OnLockCancelled()
        {
            DSM.Panels.ReleaseLockScreen(this);
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
            DSM.Panels.ReleaseLockScreen(this);
            if (IsLocking) _lockScreen!.Close();
            CloseConfirmDialog();
            Rebuild();
            RebuildCommands();
            _commandsSelected = _shownEntryCount == 0 && _shownCommands.Count > 0;
            ApplyTabs();
            if (_root != null) _root.SetActive(true);
            SetOpen(true);
        }

        public void Hide()
        {
            DSM.Panels.ReleaseLockScreen(this);
            if (IsLocking) _lockScreen!.Close();
            CloseConfirmDialog();
            if (Store.Config.AutoSave && Store.IsDirty) Store.Save();
            if (_root != null) _root.SetActive(false);
            SetOpen(false);
        }

        public void SelectSettings() => SelectTab(false);

        public void SelectCommands() => SelectTab(true);

        private void SelectTab(bool commands)
        {
            if (commands && _shownCommands.Count == 0) return;
            CloseConfirmDialog();
            _commandsSelected = commands;
            ApplyTabs();
        }

        private void ApplyTabs()
        {
            if (_shownCommands.Count == 0) _commandsSelected = false;

            if (_tabBar != null) _tabBar.SetActive(_shownCommands.Count > 0);
            if (_settingsTab != null) _settingsTab.SetSelected(!_commandsSelected);
            if (_commandsTab != null) _commandsTab.SetSelected(_commandsSelected);
            if (_settingsView != null) _settingsView.SetActive(!_commandsSelected);
            if (_commandsView != null) _commandsView.SetActive(_commandsSelected);
            if (_resetButton != null) _resetButton.gameObject.SetActive(!_commandsSelected);
            ApplySaveButtonMode();
        }

        private void OnCommandsChanged()
        {
            if (!IsVisible) return;
            Rebuild();
            RebuildCommands();
            ApplyTabs();
        }

        private void OnResetClicked()
        {
            if (_confirmDialog == null)
            {
                Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': no confirm dialog assigned; resetting without confirmation.", this);
                ResetShownEntries();
                return;
            }
            OpenConfirmDialog(_resetConfirmMessage, _resetConfirmButtonText, ResetShownEntries);
        }

        private void ResetShownEntries() => Store.ResetExposedEntries(Definition.AudienceIds);

        private void OnRunClicked(DSMRegisteredCommand command)
        {
            if (!command.Confirm)
            {
                command.Run();
                return;
            }
            if (_confirmDialog == null)
            {
                Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': no confirm dialog assigned; running '{command.Title}' without confirmation.", this);
                command.Run();
                return;
            }
            OpenConfirmDialog($"Run \u201C{DSM.Commands.DisplayTitle(command)}\u201D?", "Run", command.Run);
        }

        private void OnEntryActionClicked(DSMRegisteredEntryAction action)
        {
            void Run() => action.Run(Store);

            if (!action.Confirm)
            {
                Run();
                return;
            }
            if (_confirmDialog == null)
            {
                Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': no confirm dialog assigned; running '{action.Title}' without confirmation.", this);
                Run();
                return;
            }
            OpenConfirmDialog($"Run \u201C{DSM.Commands.DisplayTitle(action)}\u201D?", "Run", Run);
        }

        private void AddEntryActions(GameObject widgetRow, string key)
        {
            var registry = DSM.Commands;
            var actions = registry.EntryActionsFor(key);
            if (actions.Count == 0 || _widgetConfig == null || _container == null) return;

            var barPrefab = _widgetConfig.EntryActionBar;
            if (barPrefab == null)
            {
                if (!_warnedMissingEntryActionBar)
                {
                    _warnedMissingEntryActionBar = true;
                    Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': Entry Actions are skipped because the Widget Config has no Entry Action Bar prefab.", this);
                }
                return;
            }

            var rowGroup = widgetRow.TryGetComponent<CanvasGroup>(out var group) ? group : widgetRow.AddComponent<CanvasGroup>();

            DSMEntryActionError? error = null;
            var errorPrefab = _widgetConfig.EntryActionError;
            if (errorPrefab != null)
            {
                error = Instantiate(errorPrefab, _container);
                error.transform.SetSiblingIndex(widgetRow.transform.GetSiblingIndex() + 1);
                error.ApplyLayout(Layout);
            }

            var bar = Instantiate(barPrefab, widgetRow.transform);
            bar.Setup(actions, registry.DisplayTitle, OnEntryActionClicked, rowGroup, error);
        }

        private void OpenConfirmDialog(string message, string buttonText, Action onConfirm)
        {
            if (_confirmDialog == null) return;
            if (_confirmMessage != null) _confirmMessage.text = message;
            if (_confirmButtonLabel != null) _confirmButtonLabel.text = buttonText;
            _pendingConfirm = onConfirm;
            _confirmDialog.SetActive(true);
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
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
        }

        private void ApplySaveButtonMode()
        {
            if (_saveButton == null) return;

            var active = Definition.SaveButtonMode switch
            {
                DSMSaveButtonMode.Always => true,
                DSMSaveButtonMode.Never  => false,
                _                        => !Store.Config.AutoSave
            };
            _saveButton.gameObject.SetActive(active && !_commandsSelected);
        }

        private void OnDestroy()
        {
            SetOpen(false);
            if (_definition != null) DSM.Panels.Remove(this);
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
            if (_definition == null || _root == null || _container == null) return;

            if (TryGetComponent<Canvas>(out var canvas)) canvas.sortingOrder = Definition.SortOrder;
            ApplyPanelLayout();
            ApplySaveButtonMode();
            foreach (var row in _container.GetComponentsInChildren<IDSMLayoutAware>(true))
                row.ApplyLayout(Layout);
            if (_commandContainer != null)
                foreach (var row in _commandContainer.GetComponentsInChildren<IDSMLayoutAware>(true))
                    row.ApplyLayout(Layout);
        }

        private void ApplyPanelLayout()
        {
            if (_root == null) return;

            if (_root.TryGetComponent<RectTransform>(out var rect))
            {
                if (Layout.SizeMode == DSMPanelSizeMode.FullScreen)
                {
                    var padding = Layout.FullScreenPadding;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.offsetMin = new Vector2(padding, padding);
                    rect.offsetMax = new Vector2(-padding, -padding);
                }
                else
                {
                    var point = AnchorPoint(Layout.Anchor);
                    rect.anchorMin = point;
                    rect.anchorMax = point;
                    rect.pivot = point;
                    rect.sizeDelta = Layout.PanelSize;
                    rect.anchoredPosition = Layout.Offset;
                }
            }

            if (_root.TryGetComponent<Image>(out var background))
                background.color = Layout.BackgroundColor;

            ApplyContainerLayout(_container);
            ApplyContainerLayout(_commandContainer);
            if (_settingsTab != null) _settingsTab.ApplyLayout(Layout);
            if (_commandsTab != null) _commandsTab.ApplyLayout(Layout);
            if (_lockScreen != null) _lockScreen.ApplyLayout(Layout);
        }

        private void ApplyContainerLayout(Transform? container)
        {
            if (container == null) return;

            if (container.TryGetComponent<VerticalLayoutGroup>(out var group))
                group.spacing = Layout.RowSpacing;

            var scrollRect = container.GetComponentInParent<ScrollRect>(true);
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = Layout.ScrollSensitivity;
                if (scrollRect.verticalScrollbar != null
                    && scrollRect.verticalScrollbar.TryGetComponent<RectTransform>(out var barRect))
                {
                    var size = barRect.sizeDelta;
                    size.x = Layout.ScrollbarWidth;
                    barRect.sizeDelta = size;
                }
            }
        }

        private static Vector2 AnchorPoint(DSMPanelAnchor anchor) => anchor switch
        {
            DSMPanelAnchor.Left        => new Vector2(0f, 0.5f),
            DSMPanelAnchor.Right       => new Vector2(1f, 0.5f),
            DSMPanelAnchor.Top         => new Vector2(0.5f, 1f),
            DSMPanelAnchor.Bottom      => new Vector2(0.5f, 0f),
            DSMPanelAnchor.TopLeft     => new Vector2(0f, 1f),
            DSMPanelAnchor.TopRight    => new Vector2(1f, 1f),
            DSMPanelAnchor.BottomLeft  => new Vector2(0f, 0f),
            DSMPanelAnchor.BottomRight => new Vector2(1f, 0f),
            _                          => new Vector2(0.5f, 0.5f)
        };

        public void Rebuild()
        {
            ApplyPanelLayout();
            _shownEntryCount = 0;

            if (_container == null) return;

            foreach (Transform child in _container)
                Destroy(child.gameObject);

            if (_widgetConfig == null)
            {
                Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': _widgetConfig is not assigned in the Inspector.", this);
                return;
            }

            var store = Store;
            var warnedMissingHeaderRow = false;

            foreach (var entry in store.Config.Entries)
            {
                if (!DSMAudienceFilter.Shows(entry, Definition.AudienceIds)) continue;
                _shownEntryCount++;

                var presentation = DSMPresentation.Present(DSMHintSettings.From(entry));

                if (presentation.RangeMessage != null)
                    Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': entry '{entry.Key}': {presentation.RangeMessage}", this);

                if (presentation.OptionsMessage != null)
                    Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': entry '{entry.Key}': {presentation.OptionsMessage}", this);

                var widgetPrefab = _widgetConfig.GetWidgetPrefab(presentation.Widget, entry.Type);
                if (widgetPrefab == null)
                {
                    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': no widget prefab configured for '{entry.Key}' ({entry.Type}).", this);
                    continue;
                }

                if (presentation.Header != null)
                {
                    var headerPrefab = _widgetConfig.HeaderRow;
                    if (headerPrefab != null)
                    {
                        var headerRow = Instantiate(headerPrefab, _container);
                        headerRow.SetText(presentation.Header);
                        headerRow.ApplyLayout(Layout);
                    }
                    else if (!warnedMissingHeaderRow)
                    {
                        warnedMissingHeaderRow = true;
                        Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': headers are skipped because the Widget Config has no Header Row prefab.", this);
                    }
                }

                var go = Instantiate(widgetPrefab.gameObject, _container);
                var widget = go.GetComponent<IDSMWidget>();
                if (widget == null)
                {
                    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': widget prefab for '{entry.Key}' has no IDSMWidget component.", this);
                    continue;
                }
                widget.Setup(entry, store);
                AddEntryActions(go, entry.Key);
                if (widget is IDSMLayoutAware layoutAware) layoutAware.ApplyLayout(Layout);
                foreach (var bar in go.GetComponentsInChildren<DSMEntryActionBar>(true)) bar.ApplyLayout(Layout);
            }
        }

        /// <summary>Lists the Registered Commands this Panel shows on its Commands tab.</summary>
        public void RebuildCommands()
        {
            _shownCommands.Clear();
            var registry = DSM.Commands;
            var config = Store.Config;
            foreach (var command in registry.Commands)
                if (DSMAudienceFilter.Shows(command, Definition.AudienceIds, config)) _shownCommands.Add(command);

            if (_commandContainer == null) return;

            foreach (Transform child in _commandContainer)
                Destroy(child.gameObject);

            if (_shownCommands.Count == 0 || _widgetConfig == null) return;

            var rowPrefab = _widgetConfig.CommandRow;
            if (rowPrefab == null)
            {
                if (!_warnedMissingCommandRow)
                {
                    _warnedMissingCommandRow = true;
                    Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': Commands are skipped because the Widget Config has no Command Row prefab.", this);
                }
                return;
            }

            var headerPrefab = _widgetConfig.HeaderRow;
            string? previousHeader = null;
            foreach (var command in _shownCommands)
            {
                var startsGroup = command.Header != null && !string.Equals(command.Header, previousHeader, StringComparison.Ordinal);
                previousHeader = command.Header;
                if (startsGroup && headerPrefab != null)
                {
                    var headerRow = Instantiate(headerPrefab, _commandContainer);
                    headerRow.SetText(command.Header);
                    headerRow.ApplyLayout(Layout);
                }

                var row = Instantiate(rowPrefab, _commandContainer);
                row.Setup(command, registry.DisplayTitle(command), OnRunClicked);
                row.ApplyLayout(Layout);
            }
        }
    }
}
