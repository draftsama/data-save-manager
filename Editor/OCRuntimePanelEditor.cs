#nullable enable

using UnityEditor;

namespace OperatorConsole.Editor
{
    [CustomEditor(typeof(OCRuntimePanel))]
    internal sealed class OCRuntimePanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var panel = (OCRuntimePanel)target;
            if (panel.gameObject.scene.IsValid() && !EditorApplication.isPlaying)
                EditorGUILayout.HelpBox(
                    "Runtime Panels are created from Panel Definitions when the app starts. This one sits in a scene and stays hidden: add a Panel Definition under Operator Console › Runtime Panels, then remove this panel from the scene.",
                    MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Audiences, Open Triggers, Panel Lock and Layout are set on Panel Definitions in the Operator Console.", MessageType.Info);

            DrawDefaultInspector();
        }
    }
}
