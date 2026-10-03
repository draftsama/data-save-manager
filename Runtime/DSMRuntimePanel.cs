#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DataSaveManager
{
    /// <summary>The in-game UI where an operator views and edits Exposed Entries while the app runs. Toggled with a hotkey.</summary>
    public sealed class DSMRuntimePanel : MonoBehaviour
    {
        [SerializeField] private DSMWidgetConfig? _widgetConfig;
        [SerializeField] private GameObject? _root;
        [SerializeField] private Transform? _container;
        [SerializeField] private Button? _saveButton;
        [SerializeField] private Button? _resetAllButton;
        [SerializeField] private Button? _closeButton;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private bool _startVisible;

#if ENABLE_INPUT_SYSTEM
        private Key _inputSystemToggleKey;
        private bool _hasInputSystemToggleKey;
#endif

        public static bool IsOpen { get; private set; }
        public static event Action<bool>? OpenChanged;

        public bool IsVisible => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_saveButton != null) _saveButton.onClick.AddListener(DSM.Save);
            if (_resetAllButton != null) _resetAllButton.onClick.AddListener(DSM.ResetAll);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);

#if ENABLE_INPUT_SYSTEM
            // Parsed once here instead of every frame in Update.
            _hasInputSystemToggleKey = Enum.TryParse<Key>(_toggleKey.ToString(), out _inputSystemToggleKey);
#endif

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
            Rebuild();
            if (_root != null) _root.SetActive(true);
            SetOpen(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            SetOpen(false);
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

        public void Rebuild()
        {
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
                var widgetPrefab = _widgetConfig.GetWidgetPrefab(entry.Type);
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
            }
        }
    }
}
