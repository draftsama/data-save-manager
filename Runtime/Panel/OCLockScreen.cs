#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OperatorConsole
{
    /// <summary>The full-screen keypad a Panel Lock shows before its Runtime Panel opens.</summary>
    public sealed class OCLockScreen : MonoBehaviour, IOCLayoutAware
    {
        private const float CheckDelay = 0.12f;
        private const float ShakeDuration = 0.4f;
        private const float ShakeAmplitude = 14f;
        private const float ShakeFrequency = 9f;

        [SerializeField] private Image? _backdrop;
        [SerializeField, Tooltip("Digit keys, indexed by the digit they enter (element 0 is the 0 key).")]
        private Button[] _digitKeys = Array.Empty<Button>();
        [SerializeField] private Button? _cancelKey;
        [SerializeField] private Button? _backspaceKey;
        [SerializeField] private GridLayoutGroup? _keypad;
        [SerializeField, Tooltip("One dot per possible digit; only as many as the Passcode has are shown.")]
        private Image[] _dots = Array.Empty<Image>();
        [SerializeField] private RectTransform? _dotRow;
        [SerializeField] private LayoutElement? _dotRowSlot;
        [SerializeField] private TMP_Text? _error;
        [SerializeField] private string _wrongPasscodeText = "Wrong passcode";

        private enum Phase { Entering, Checking, Shaking }

        private readonly char[] _entered = new char[OCPasscode.MaxLength];
        private int _count;
        private Phase _phase;
        private float _phaseStart;
        private OCPasscode? _passcode;
        private Action? _onUnlocked;
        private Action? _onCancelled;

#if ENABLE_INPUT_SYSTEM
        private static readonly Key[] DigitRowKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        private static readonly Key[] NumpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
#endif

        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            for (var i = 0; i < _digitKeys.Length; i++)
            {
                var key = _digitKeys[i];
                if (key == null) continue;
                var digit = (char)('0' + i);
                key.onClick.AddListener(() => PressDigit(digit));
                if (key.targetGraphic is Image image) image.sprite = OCCircleSprites.Disc;
            }
            if (_cancelKey != null) _cancelKey.onClick.AddListener(Cancel);
            if (_backspaceKey != null) _backspaceKey.onClick.AddListener(PressBackspace);
        }

        /// <summary>Shows the keypad for <paramref name="passcode"/>. Exactly one of the callbacks runs, unless <see cref="Close"/> is called first.</summary>
        public void Open(OCPasscode passcode, Action onUnlocked, Action onCancelled)
        {
            _passcode = passcode;
            _onUnlocked = onUnlocked;
            _onCancelled = onCancelled;
            ClearEntry();
            if (_error != null) _error.text = string.Empty;
            for (var i = 0; i < _dots.Length; i++)
                if (_dots[i] != null) _dots[i].gameObject.SetActive(i < passcode.Length);
            gameObject.SetActive(true);
        }

        /// <summary>Hides the keypad without running either callback.</summary>
        public void Close()
        {
            _passcode = null;
            _onUnlocked = null;
            _onCancelled = null;
            ClearEntry();
            gameObject.SetActive(false);
        }

        /// <summary>Hides the keypad and runs the cancel callback.</summary>
        public void Cancel()
        {
            if (!IsShown) return;
            var onCancelled = _onCancelled;
            Close();
            onCancelled?.Invoke();
        }

        public void PressDigit(char digit)
        {
            if (_phase != Phase.Entering || _passcode == null || _count >= _passcode.Length) return;
            if (_count == 0 && _error != null) _error.text = string.Empty;
            _entered[_count++] = digit;
            RefreshDots();
            if (_count == _passcode.Length) StartPhase(Phase.Checking);
        }

        public void PressBackspace()
        {
            if (_phase != Phase.Entering || _count == 0) return;
            _count--;
            RefreshDots();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            if (_backdrop != null)
            {
                var color = _backdrop.color;
                color.a = layout.LockBackgroundAlpha;
                _backdrop.color = color;
            }

            if (_keypad != null)
            {
                _keypad.cellSize = new Vector2(layout.LockKeySize, layout.LockKeySize);
                _keypad.spacing = new Vector2(layout.LockKeySpacing, layout.LockKeySpacing);
            }
            foreach (var key in _digitKeys) SetLabelSize(key, layout.LockKeyFontSize);
            SetLabelSize(_cancelKey, layout.LockKeyFontSize * 0.5f);
            SetLabelSize(_backspaceKey, layout.LockKeyFontSize * 0.5f);

            foreach (var dot in _dots)
            {
                if (dot == null || !dot.TryGetComponent<LayoutElement>(out var element)) continue;
                element.preferredWidth = layout.LockDotSize;
                element.preferredHeight = layout.LockDotSize;
            }
            if (_dotRow != null && _dotRow.TryGetComponent<HorizontalLayoutGroup>(out var row)) row.spacing = layout.LockDotSize * 1.1f;
            if (_dotRowSlot != null) _dotRowSlot.preferredHeight = layout.LockDotSize;
        }

        private static void SetLabelSize(Button? key, float size)
        {
            if (key == null) return;
            var label = key.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = size;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Entering:
                    ReadKeyboard();
                    break;
                case Phase.Checking:
                    if (Time.unscaledTime - _phaseStart >= CheckDelay) Check();
                    break;
                case Phase.Shaking:
                    var t = (Time.unscaledTime - _phaseStart) / ShakeDuration;
                    if (t >= 1f)
                    {
                        SetDotRowOffset(0f);
                        ClearEntry();
                    }
                    else
                    {
                        SetDotRowOffset(Mathf.Sin(t * ShakeFrequency * Mathf.PI * 2f) * ShakeAmplitude * (1f - t));
                    }
                    break;
            }
        }

        private void Check()
        {
            if (_passcode != null && _passcode.Matches(new string(_entered, 0, _count)))
            {
                var onUnlocked = _onUnlocked;
                Close();
                onUnlocked?.Invoke();
                return;
            }

            if (_error != null) _error.text = _wrongPasscodeText;
            StartPhase(Phase.Shaking);
        }

        private void ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (var i = 0; i < 10; i++)
                if (keyboard[DigitRowKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                    PressDigit((char)('0' + i));
            if (keyboard.backspaceKey.wasPressedThisFrame) PressBackspace();
            if (keyboard.escapeKey.wasPressedThisFrame) Cancel();
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (var i = 0; i < 10; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                    PressDigit((char)('0' + i));
            if (Input.GetKeyDown(KeyCode.Backspace)) PressBackspace();
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
#endif
        }

        private void StartPhase(Phase phase)
        {
            _phase = phase;
            _phaseStart = Time.unscaledTime;
        }

        private void ClearEntry()
        {
            _count = 0;
            _phase = Phase.Entering;
            SetDotRowOffset(0f);
            RefreshDots();
        }

        private void RefreshDots()
        {
            for (var i = 0; i < _dots.Length; i++)
                if (_dots[i] != null) _dots[i].sprite = i < _count ? OCCircleSprites.Disc : OCCircleSprites.Ring;
        }

        private void SetDotRowOffset(float x)
        {
            if (_dotRow == null) return;
            var position = _dotRow.anchoredPosition;
            position.x = x;
            _dotRow.anchoredPosition = position;
        }
    }
}
