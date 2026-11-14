#nullable enable

using UnityEditor;

namespace DataSaveManager.Editor
{
    [CustomEditor(typeof(DSMRuntimePanel))]
    internal sealed class DSMRuntimePanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var panel = (DSMRuntimePanel)target;
            if (panel.gameObject.scene.IsValid() && !EditorApplication.isPlaying)
                EditorGUILayout.HelpBox(
                    "Runtime Panels are created from Panel Definitions when the app starts. This one sits in a scene and stays hidden: import it with DSM Manager › Runtime Panels › Import Scene Panels, then remove it from the scene.",
                    MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Audiences, Open Triggers, Panel Lock and Layout are set on Panel Definitions in the DSM Manager.", MessageType.Info);

            DrawDefaultInspector();
        }
    }
}
