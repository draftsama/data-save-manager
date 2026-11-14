#nullable enable

using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Sets, changes and removes a Panel Lock's Passcode, writing only its salted hash.</summary>
    internal static class DSMPasscodeEditing
    {
        private static readonly GUIContent NewPasscodeLabel = new("New Passcode", "4–8 digits. Only a salted hash is stored; the digits cannot be read back.");

        public static void Draw(SerializedProperty passcode, ref string newPasscode, bool hasLockScreen)
        {
            var length = passcode.FindPropertyRelative("_length");
            var salt = passcode.FindPropertyRelative("_salt");
            var hash = passcode.FindPropertyRelative("_hash");
            var isSet = length.intValue > 0 && !string.IsNullOrEmpty(hash.stringValue);

            EditorGUILayout.LabelField("Passcode", isSet ? $"Set ({length.intValue} digits)" : "None — Open Triggers open the panel directly");

            newPasscode = EditorGUILayout.TextField(NewPasscodeLabel, newPasscode);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!DSMPasscode.IsValid(newPasscode)))
                {
                    if (GUILayout.Button(isSet ? "Change Passcode" : "Set Passcode", GUILayout.Width(140f)))
                    {
                        var newSalt = DSMPasscode.NewSalt();
                        salt.stringValue = newSalt;
                        hash.stringValue = DSMPasscode.Hash(newPasscode, newSalt);
                        length.intValue = newPasscode.Length;
                        newPasscode = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
                using (new EditorGUI.DisabledScope(!isSet))
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                    {
                        salt.stringValue = string.Empty;
                        hash.stringValue = string.Empty;
                        length.intValue = 0;
                    }
                }
            }

            if (newPasscode.Length > 0 && !DSMPasscode.IsValid(newPasscode))
                EditorGUILayout.HelpBox($"A Passcode is {DSMPasscode.MinLength}–{DSMPasscode.MaxLength} digits.", MessageType.None);

            if (isSet && !hasLockScreen)
                EditorGUILayout.HelpBox("A Passcode is set but the panel prefab has no Lock Screen: Open Triggers will not open this panel.", MessageType.Error);
        }
    }
}
