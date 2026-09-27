#nullable enable

using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    [CustomEditor(typeof(DSMConfig))]
    internal sealed class DSMConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open DSM Manager"))
                DSMManagerWindow.Open();

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
