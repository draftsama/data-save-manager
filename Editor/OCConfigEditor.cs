#nullable enable

using UnityEditor;
using UnityEngine;

namespace OperatorConsole.Editor
{
    [CustomEditor(typeof(OCConfig))]
    internal sealed class OCConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Operator Console"))
                OCManagerWindow.Open();

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
