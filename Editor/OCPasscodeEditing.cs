#nullable enable

using UnityEditor;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>Sets, changes and removes a Panel Lock's Passcode, writing only its salted hash.</summary>
    internal static class OCPasscodeEditing
    {
        public static VisualElement Create(SerializedProperty passcode)
        {
            var length = passcode.FindPropertyRelative("_length");
            var salt = passcode.FindPropertyRelative("_salt");
            var hash = passcode.FindPropertyRelative("_hash");

            var root = new VisualElement();
            var status = new Label();
            status.AddToClassList("oc-muted");
            root.Add(status);

            var row = new VisualElement();
            row.AddToClassList("oc-row");
            var input = new TextField("New Passcode") { maxLength = OCPasscode.MaxLength, tooltip = "4–8 digits. Only a salted hash is stored; the digits cannot be read back." };
            input.AddToClassList("oc-grow");
            var set = new Button();
            var remove = new Button { text = "Remove" };
            row.Add(input);
            row.Add(set);
            row.Add(remove);
            root.Add(row);

            var hint = new Label($"A Passcode is {OCPasscode.MinLength}–{OCPasscode.MaxLength} digits.");
            hint.AddToClassList("oc-error-text");
            root.Add(hint);

            void Refresh()
            {
                var isSet = length.intValue > 0 && !string.IsNullOrEmpty(hash.stringValue);
                status.text = isSet ? $"Passcode set ({length.intValue} digits)" : "No Passcode: Open Triggers open the panel directly";
                set.text = isSet ? "Change Passcode" : "Set Passcode";
                set.SetEnabled(OCPasscode.IsValid(input.value));
                remove.SetEnabled(isSet);
                hint.style.display = input.value.Length > 0 && !OCPasscode.IsValid(input.value) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            input.RegisterValueChangedCallback(_ => Refresh());
            set.clicked += () =>
            {
                var newSalt = OCPasscode.NewSalt();
                salt.stringValue = newSalt;
                hash.stringValue = OCPasscode.Hash(input.value, newSalt);
                length.intValue = input.value.Length;
                passcode.serializedObject.ApplyModifiedProperties();
                input.SetValueWithoutNotify(string.Empty);
                Refresh();
            };
            remove.clicked += () =>
            {
                salt.stringValue = string.Empty;
                hash.stringValue = string.Empty;
                length.intValue = 0;
                passcode.serializedObject.ApplyModifiedProperties();
                Refresh();
            };

            Refresh();
            return root;
        }
    }
}
