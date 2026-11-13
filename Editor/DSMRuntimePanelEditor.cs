#nullable enable

using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    [CustomEditor(typeof(DSMRuntimePanel))]
    internal sealed class DSMRuntimePanelEditor : UnityEditor.Editor
    {
        private static readonly GUIContent AudiencesLabel = new("Audiences", "Exposed Entries this Panel shows: those naming any of these Audiences, plus those naming none. None selected: every Exposed Entry.");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "_audienceIds");

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
    }
}
