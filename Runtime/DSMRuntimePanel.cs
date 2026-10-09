#nullable enable

using System;
using System.Linq;
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

    /// <summary>The in-game UI where an operator views and edits Exposed Entries while the app runs. Toggled with a hotkey.</summary>
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

#if ENABLE_INPUT_SYSTEM
        private Key _inputSystemToggleKey;
        private bool _hasInputSystemToggleKey;
#endif

        public static bool IsOpen { get; private set; }
        public static event Action<bool>? OpenChanged;

        public DSMPanelLayout Layout => _layout;

        public bool IsVisible => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_saveButton != null) _saveButton.onClick.AddListener(DSM.Save);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_confirmCancelButton != null) _confirmCancelButton.onClick.AddListener(CloseConfirmDialog);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(OnConfirmResetClicked);

#if ENABLE_INPUT_SYSTEM
            // Parsed once here instead of every frame in Update.
            _hasInputSystemToggleKey = Enum.TryParse<Key>(_toggleKey.ToString(), out _inputSystemToggleKey);
#endif

            CloseConfirmDialog();
            ApplySaveButtonMode();

            if (_startVisible) Show();
            else if (_root != null) _root.SetActive(false);
        }

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
            ApplySaveButtonMode();
            Rebuild();
            if (_root != null) _root.SetActive(true);
            SetOpen(true);
        }

        public void Hide()
        {
            CloseConfirmDialog();
            if (DSM.Config.AutoSave && DSM.Store.IsDirty) DSM.Save();
            if (_root != null) _root.SetActive(false);
            SetOpen(false);
        }

        private void OnResetClicked()
        {
            if (_confirmDialog == null)
            {
                Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': no confirm dialog assigned; resetting without confirmation.", this);
                ResetExposedEntries();
                return;
            }
            _confirmDialog.SetActive(true);
        }

        private void OnConfirmResetClicked()
        {
            CloseConfirmDialog();
            ResetExposedEntries();
        }

        private void CloseConfirmDialog()
        {
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
        }

        // Exposed Entries only: ResetAll would also wipe ad-hoc keys (e.g. window-layout keys).
        private static void ResetExposedEntries()
        {
            foreach (var entry in DSM.Config.Entries.Where(e => e.Exposed))
                DSM.Reset(entry.Key);
        }

        private void ApplySaveButtonMode()
        {
            if (_saveButton == null) return;

            var active = _saveButtonMode switch
            {
                DSMSaveButtonMode.Always => true,
                DSMSaveButtonMode.Never  => false,
                _                        => !DSM.Config.AutoSave
            };
            _saveButton.gameObject.SetActive(active);
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

            if (_container != null && _container.TryGetComponent<VerticalLayoutGroup>(out var group))
                group.spacing = _layout.RowSpacing;

            var scrollRect = _container != null ? _container.GetComponentInParent<ScrollRect>(true) : null;
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

            if (_container == null) return;

            foreach (Transform child in _container)
                Destroy(child.gameObject);

            if (_widgetConfig == null)
            {
                Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': _widgetConfig is not assigned in the Inspector.", this);
                return;
            }

            foreach (var entry in DSM.Config.Entries.Where(e => e.Exposed))
            {
                if (entry.HasRange && !entry.HasValidRange && (entry.Type is DSMDataType.Int or DSMDataType.Float))
                    Debug.LogWarning($"DSMRuntimePanel on '{gameObject.name}': entry '{entry.Key}' has an invalid Range (min must be less than max); using the plain input widget.", this);

                var widgetPrefab = _widgetConfig.GetWidgetPrefab(entry);
                if (widgetPrefab == null)
                {
                    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': no widget prefab configured for '{entry.Key}' ({entry.Type}).", this);
                    continue;
                }

                var go = Instantiate(widgetPrefab.gameObject, _container);
                var widget = go.GetComponent<IDSMWidget>();
                if (widget == null)
                {
                    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': widget prefab for '{entry.Key}' has no IDSMWidget component.", this);
                    continue;
                }
                widget.Setup(entry);
                if (widget is IDSMLayoutAware layoutAware) layoutAware.ApplyLayout(_layout);
            }
        }
    }
}
