#nullable enable

#if ENABLE_INPUT_SYSTEM
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMKeyMapTests
    {
        [TestCase(KeyCode.F1, Key.F1)]
        [TestCase(KeyCode.A, Key.A)]
        [TestCase(KeyCode.Alpha0, Key.Digit0)]
        [TestCase(KeyCode.Alpha1, Key.Digit1)]
        [TestCase(KeyCode.Alpha9, Key.Digit9)]
        [TestCase(KeyCode.Keypad5, Key.Numpad5)]
        [TestCase(KeyCode.Return, Key.Enter)]
        [TestCase(KeyCode.BackQuote, Key.Backquote)]
        [TestCase(KeyCode.LeftControl, Key.LeftCtrl)]
        [TestCase(KeyCode.KeypadEnter, Key.NumpadEnter)]
        [TestCase(KeyCode.LeftWindows, Key.LeftMeta)]
        public void KeyboardKeyCode_MapsToSameKey(KeyCode keyCode, Key expected)
        {
            Assert.IsTrue(DSMKeyMap.TryGetKey(keyCode, out var key));
            Assert.AreEqual(expected, key);
        }

        [TestCase(KeyCode.None)]
        [TestCase(KeyCode.Mouse0)]
        [TestCase(KeyCode.JoystickButton0)]
        public void NonKeyboardKeyCode_DoesNotMap(KeyCode keyCode)
        {
            Assert.IsFalse(DSMKeyMap.TryGetKey(keyCode, out _));
        }
    }
}
#endif
