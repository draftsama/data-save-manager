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

    /// <summary>The in-game UI where an operator views and edits Exposed Entries and runs Commands and Entry Actions while the app runs. Toggled with a hotkey.</summary>
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
        [SerializeField, Tooltip("Auto shows the Save button only when AutoSave is off. Always / Never force it on / off.")]
        private DSMSaveButtonMode _saveButtonMode = DSMSaveButtonMode.Auto;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private bool _startVisible;
        [SerializeField] private DSMPanelLayout _layout = new();
        [SerializeField] private List<string> _audienceIds = new();
        [SerializeField] private GameObject? _tabBar;
        [SerializeField] private DSMPanelTab? _settingsTab;
        [SerializeField] private DSMPanelTab? _commandsTab;
        [SerializeField] private GameObject? _settingsView;
        [SerializeField] private GameObject? _commandsView;
        [SerializeField] private Transform? _commandContainer;
        [SerializeField] private TMP_Text? _confirmMessage;
        [SerializeField] private TMP_Text? _confirmButtonLabel;

        private readonly List<DSMRegisteredCommand> _shownCommands = new();
        private int _shownEntryCount;
        private bool _commandsSelected;
        private Action? _pendingConfirm;
        private string _resetConfirmMessage = string.Empty;
        private string _resetConfirmButtonText = string.Empty;
        private bool _warnedMissingCommandRow;
        private bool _warnedMissingEntryActionBar;

#if ENABLE_INPUT_SYSTEM
        private Key _inputSystemToggleKey;
        private bool _hasInputSystemToggleKey;
#endif

        public static bool IsOpen { get; private set; }
        public static event Action<bool>? OpenChanged;

        public DSMPanelLayout Layout => _layout;

        /// <summary>Ids of the Audiences this Panel serves; empty means it shows every Exposed Entry.</summary>
        public IReadOnlyList<string> AudienceIds => _audienceIds;

        public bool IsVisible => _root != null && _root.activeSelf;

        private static DSMStore Store => DSM.Store;

        private static void SaveStore() => Store.Save();

        private void Awake()
        {
            if (_saveButton != null) _saveButton.onClick.AddListener(SaveStore);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_confirmCancelButton != null) _confirmCancelButton.onClick.AddListener(CloseConfirmDialog);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(OnConfirmClicked);
            if (_settingsTab != null && _settingsTab.Button != null) _settingsTab.Button.onClick.AddListener(SelectSettings);
            if (_commandsTab != null && _commandsTab.Button != null) _commandsTab.Button.onClick.AddListener(SelectCommands);
            if (_confirmMessage != null) _resetConfirmMessage = _confirmMessage.text;
            if (_confirmButtonLabel != null) _resetConfirmButtonText = _confirmButtonLabel.text;

#if ENABLE_INPUT_SYSTEM
            // Parsed once here instead of every frame in Update.
            _hasInputSystemToggleKey = Enum.TryParse<Key>(_toggleKey.ToString(), out _inputSystemToggleKey);
#endif

            CloseConfirmDialog();
            ApplySaveButtonMode();

            if (_startVisible) Show();
            else if (_root != null) _root.SetActive(false);
        }

        private void OnEnable() => DSM.Commands.Changed += OnCommandsChanged;

        private void OnDisable() => DSM.Commands.Changed -= OnCommandsChanged;

        private void Update()
        {
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = _hasInputSystemToggleKey && Keyboard.current != null && Keyboard.current[_inputSystemToggleKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(_toggleKey);
#else
            pressed = false;
#endif
            if (pressed) Toggle();
        }

        public void Show()
        {
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

        private void ResetShownEntries() => Store.ResetExposedEntries(_audienceIds);

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
                error.ApplyLayout(_layout);
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

            var active = _saveButtonMode switch
            {
                DSMSaveButtonMode.Always => true,
                DSMSaveButtonMode.Never  => false,
                _                        => !Store.Config.AutoSave
            };
            _saveButton.gameObject.SetActive(active && !_commandsSelected);
        }

        private void OnDestroy() => SetOpen(false);

        private static void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            OpenChanged?.Invoke(open);
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || _root == null || _container == null) return;

            ApplyPanelLayout();
            ApplySaveButtonMode();
            foreach (var row in _container.GetComponentsInChildren<IDSMLayoutAware>(true))
                row.ApplyLayout(_layout);
            if (_commandContainer != null)
                foreach (var row in _commandContainer.GetComponentsInChildren<IDSMLayoutAware>(true))
                    row.ApplyLayout(_layout);
        }

        private void ApplyPanelLayout()
        {
            if (_root == null) return;

            if (_root.TryGetComponent<RectTransform>(out var rect))
            {
                var point = AnchorPoint(_layout.Anchor);
                rect.anchorMin = point;
                rect.anchorMax = point;
                rect.pivot = point;
                rect.sizeDelta = _layout.PanelSize;
                rect.anchoredPosition = _layout.Offset;
            }

            if (_root.TryGetComponent<Image>(out var background))
                background.color = _layout.BackgroundColor;

            ApplyContainerLayout(_container);
            ApplyContainerLayout(_commandContainer);
            if (_settingsTab != null) _settingsTab.ApplyLayout(_layout);
            if (_commandsTab != null) _commandsTab.ApplyLayout(_layout);
        }

        private void ApplyContainerLayout(Transform? container)
        {
            if (container == null) return;

            if (container.TryGetComponent<VerticalLayoutGroup>(out var group))
                group.spacing = _layout.RowSpacing;

            var scrollRect = container.GetComponentInParent<ScrollRect>(true);
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = _layout.ScrollSensitivity;
                if (scrollRect.verticalScrollbar != null
                    && scrollRect.verticalScrollbar.TryGetComponent<RectTransform>(out var barRect))
                {
                    var size = barRect.sizeDelta;
                    size.x = _layout.ScrollbarWidth;
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
                if (!DSMAudienceFilter.Shows(entry, _audienceIds)) continue;
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
                        headerRow.ApplyLayout(_layout);
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
                if (widget is IDSMLayoutAware layoutAware) layoutAware.ApplyLayout(_layout);
                foreach (var bar in go.GetComponentsInChildren<DSMEntryActionBar>(true)) bar.ApplyLayout(_layout);
            }
        }

        /// <summary>Lists the Registered Commands this Panel shows on its Commands tab.</summary>
        public void RebuildCommands()
        {
            _shownCommands.Clear();
            var registry = DSM.Commands;
            var config = Store.Config;
            foreach (var command in registry.Commands)
                if (DSMAudienceFilter.Shows(command, _audienceIds, config)) _shownCommands.Add(command);

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
                    headerRow.ApplyLayout(_layout);
                }

                var row = Instantiate(rowPrefab, _commandContainer);
                row.Setup(command, registry.DisplayTitle(command), OnRunClicked);
                row.ApplyLayout(_layout);
            }
        }
    }
}
