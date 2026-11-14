#nullable enable

#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OperatorConsole
{
    /// <summary>Maps a legacy <see cref="KeyCode"/> to the Input System <see cref="Key"/> on the same physical keyboard key.</summary>
    public static class OCKeyMap
    {
        /// <summary>False for mouse and joystick buttons and keys the Input System has no equivalent for.</summary>
        public static bool TryGetKey(KeyCode keyCode, out Key key)
        {
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                key = keyCode == KeyCode.Alpha0 ? Key.Digit0 : Key.Digit1 + (keyCode - KeyCode.Alpha1);
                return true;
            }
            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                key = Key.Numpad0 + (keyCode - KeyCode.Keypad0);
                return true;
            }

            key = keyCode switch
            {
                KeyCode.Return         => Key.Enter,
                KeyCode.BackQuote      => Key.Backquote,
                KeyCode.LeftControl    => Key.LeftCtrl,
                KeyCode.RightControl   => Key.RightCtrl,
                KeyCode.Numlock        => Key.NumLock,
                KeyCode.Print          => Key.PrintScreen,
                KeyCode.Menu           => Key.ContextMenu,
                KeyCode.KeypadEnter    => Key.NumpadEnter,
                KeyCode.KeypadDivide   => Key.NumpadDivide,
                KeyCode.KeypadMultiply => Key.NumpadMultiply,
                KeyCode.KeypadPlus     => Key.NumpadPlus,
                KeyCode.KeypadMinus    => Key.NumpadMinus,
                KeyCode.KeypadPeriod   => Key.NumpadPeriod,
                KeyCode.KeypadEquals   => Key.NumpadEquals,
                _                      => Key.None
            };
            if (key != Key.None) return true;

            if (keyCode != KeyCode.None
                && Enum.TryParse(keyCode.ToString(), false, out key)
                && Enum.IsDefined(typeof(Key), key)
                && key != Key.None)
                return true;

            key = Key.None;
            return false;
        }
    }
}
#endif
