#nullable enable

using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    [CustomEditor(typeof(DSMRuntimePanel))]
    internal sealed class DSMRuntimePanelEditor : UnityEditor.Editor
    {
        private static readonly GUIContent PasscodeLabel = new("New Passcode", "4–8 digits. Only a salted hash is stored; the digits cannot be read back.");

        private string _newPasscode = string.Empty;

        private static readonly GUIContent AudiencesLabel = new("Audiences", "Exposed Entries this Panel shows: those naming any of these Audiences, plus those naming none. None selected: every Exposed Entry.");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "_audienceIds", "_passcode");

            EditorGUILayout.Space();
            DrawPasscode();

            EditorGUILayout.Space();
            var idsProp = serializedObject.FindProperty("_audienceIds");
            var config = Resources.Load<DSMConfig>("DSMConfig");

            if (config == null)
            {
                EditorGUILayout.HelpBox("No DSMConfig found in Resources, so Audiences can't be picked.", MessageType.Info);
            }
            else if (config.Audiences.Count == 0)
            {
                EditorGUILayout.HelpBox("The Config has no Audiences; this Panel shows every Exposed Entry. Add Audiences in the DSM Manager.", MessageType.None);
            }
            else
            {
                var rect = EditorGUILayout.GetControlRect();
                DSMAudienceEditing.DrawMaskField(rect, AudiencesLabel, idsProp, config, DSMAudienceEditing.Names(config));

                if (idsProp.arraySize == 0)
                    EditorGUILayout.HelpBox("This Panel serves no Audience, so it shows every Exposed Entry, including ones meant for other Audiences.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPasscode()
        {
            EditorGUILayout.LabelField("Panel Lock", EditorStyles.boldLabel);
            var passcode = serializedObject.FindProperty("_passcode");
            var length = passcode.FindPropertyRelative("_length");
            var salt = passcode.FindPropertyRelative("_salt");
            var hash = passcode.FindPropertyRelative("_hash");
            var isSet = length.intValue > 0 && !string.IsNullOrEmpty(hash.stringValue);

            EditorGUILayout.LabelField("Passcode", isSet ? $"Set ({length.intValue} digits)" : "None — Open Triggers open the panel directly");

            _newPasscode = EditorGUILayout.TextField(PasscodeLabel, _newPasscode);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!DSMPasscode.IsValid(_newPasscode)))
                {
                    if (GUILayout.Button(isSet ? "Change Passcode" : "Set Passcode", GUILayout.Width(140f)))
                    {
                        var newSalt = DSMPasscode.NewSalt();
                        salt.stringValue = newSalt;
                        hash.stringValue = DSMPasscode.Hash(_newPasscode, newSalt);
                        length.intValue = _newPasscode.Length;
                        _newPasscode = string.Empty;
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

            if (_newPasscode.Length > 0 && !DSMPasscode.IsValid(_newPasscode))
                EditorGUILayout.HelpBox($"A Passcode is {DSMPasscode.MinLength}–{DSMPasscode.MaxLength} digits.", MessageType.None);

            if (isSet && serializedObject.FindProperty("_lockScreen").objectReferenceValue == null)
                EditorGUILayout.HelpBox("A Passcode is set but no Lock Screen is assigned: Open Triggers will not open this panel.", MessageType.Error);
        }
    }
}
