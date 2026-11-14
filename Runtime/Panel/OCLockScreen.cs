#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OperatorConsole
{
    /// <summary>The full-screen keypad a Panel Lock shows before its Runtime Panel opens. The owning panel calls <see cref="Tick"/> every frame.</summary>
    internal sealed class OCLockScreen : VisualElement, IOCLayoutAware
    {
        private const float CheckDelay = 0.12f;
        private const float ShakeDuration = 0.4f;
        private const float ShakeAmplitude = 14f;
        private const float ShakeFrequency = 9f;
        private const string WrongPasscodeText = "Wrong passcode";
        private const int Columns = 3;

        private enum Phase { Entering, Checking, Shaking }

        private readonly char[] _entered = new char[OCPasscode.MaxLength];
        private readonly VisualElement[] _dots = new VisualElement[OCPasscode.MaxLength];
        private readonly VisualElement _dotRow = new();
        private readonly Label _error = new();
        private readonly VisualElement _pad = new();
        private readonly Button[] _keys = new Button[12];
        private int _keyCount;
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

        public OCLockScreen()
        {
            AddToClassList("ocp-lock");
            var content = new VisualElement();
            content.style.alignItems = Align.Center;

            var title = new Label("Enter Passcode");
            title.AddToClassList("ocp-lock__title");
            content.Add(title);

            _dotRow.AddToClassList("ocp-lock__dots");
            for (var i = 0; i < _dots.Length; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("ocp-lock__dot");
                _dots[i] = dot;
                _dotRow.Add(dot);
            }
            content.Add(_dotRow);

            _error.AddToClassList("ocp-lock__error");
            content.Add(_error);

            _pad.AddToClassList("ocp-lock__pad");
            for (var digit = 1; digit <= 9; digit++) AddDigitKey(digit);
            AddKey("Cancel", ghost: true, Cancel);
            AddDigitKey(0);
            AddKey("Delete", ghost: true, PressBackspace);
            content.Add(_pad);

            Add(content);
            style.display = DisplayStyle.None;
        }

        public bool IsShown => style.display != DisplayStyle.None;

        /// <summary>Shows the keypad for <paramref name="passcode"/>. Exactly one of the callbacks runs, unless <see cref="Close"/> is called first.</summary>
        public void Open(OCPasscode passcode, Action onUnlocked, Action onCancelled)
        {
            _passcode = passcode;
            _onUnlocked = onUnlocked;
            _onCancelled = onCancelled;
            ClearEntry();
            _error.text = string.Empty;
            for (var i = 0; i < _dots.Length; i++)
                _dots[i].style.display = i < passcode.Length ? DisplayStyle.Flex : DisplayStyle.None;
            style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the keypad without running either callback.</summary>
        public void Close()
        {
            _passcode = null;
            _onUnlocked = null;
            _onCancelled = null;
            ClearEntry();
            style.display = DisplayStyle.None;
        }

        /// <summary>Hides the keypad and runs the cancel callback.</summary>
        public void Cancel()
        {
            if (style.display == DisplayStyle.None) return;
            var onCancelled = _onCancelled;
            Close();
            onCancelled?.Invoke();
        }

        public void PressDigit(char digit)
        {
            if (_phase != Phase.Entering || _passcode == null || _count >= _passcode.Length) return;
            if (_count == 0) _error.text = string.Empty;
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
            style.backgroundColor = new Color(0.03f, 0.035f, 0.047f, layout.LockBackgroundAlpha);

            var size = layout.LockKeySize;
            var spacing = layout.LockKeySpacing;
            for (var i = 0; i < _keys.Length; i++)
            {
                var key = _keys[i];
                key.style.width = size;
                key.style.height = size;
                key.style.borderTopLeftRadius = key.style.borderTopRightRadius =
                    key.style.borderBottomLeftRadius = key.style.borderBottomRightRadius = size / 2f;
                key.style.marginLeft = i % Columns == 0 ? 0f : spacing;
                key.style.marginTop = i < Columns ? 0f : spacing;
                key.style.fontSize = key.ClassListContains("ocp-lock__key--ghost") ? layout.LockKeyFontSize * 0.5f : layout.LockKeyFontSize;
            }

            var dotSize = layout.LockDotSize;
            _dotRow.style.height = dotSize;
            for (var i = 0; i < _dots.Length; i++)
            {
                var dot = _dots[i];
                dot.style.width = dotSize;
                dot.style.height = dotSize;
                dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                    dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = dotSize / 2f;
                dot.style.marginLeft = i == 0 ? 0f : dotSize * 1.1f;
            }
        }

        public void Tick()
        {
            if (style.display == DisplayStyle.None) return;
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
                    if (t >= 1f) ClearEntry();
                    else SetDotRowOffset(Mathf.Sin(t * ShakeFrequency * Mathf.PI * 2f) * ShakeAmplitude * (1f - t));
                    break;
            }
        }

        private void AddDigitKey(int digit)
        {
            var c = (char)('0' + digit);
            AddKey(digit.ToString(), ghost: false, () => PressDigit(c));
        }

        private void AddKey(string text, bool ghost, Action onClick)
        {
            var key = new Button(onClick) { text = text };
            key.AddToClassList("ocp-lock__key");
            if (ghost) key.AddToClassList("ocp-lock__key--ghost");
            // One row per three keys, so pixel rounding can never wrap a key onto the next line.
            var index = _keyCount++;
            if (index % Columns == 0)
            {
                var row = new VisualElement();
                row.AddToClassList("ocp-lock__row");
                _pad.Add(row);
            }
            _keys[index] = key;
            _pad[_pad.childCount - 1].Add(key);
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

            _error.text = WrongPasscodeText;
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
                _dots[i].EnableInClassList("ocp-lock__dot--filled", i < _count);
        }

        private void SetDotRowOffset(float x) => _dotRow.style.translate = new Translate(x, 0f);
    }
}
