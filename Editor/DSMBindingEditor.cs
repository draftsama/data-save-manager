#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    [CustomEditor(typeof(DSMBinding))]
    internal sealed class DSMBindingEditor : UnityEditor.Editor
    {
        private const string ConfigResourcePath = "DSMConfig";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var binding = (DSMBinding)target;
            var gameObject = binding.gameObject;
            var config = Resources.Load<DSMConfig>(ConfigResourcePath);

            var keyProp = serializedObject.FindProperty("_key");
            var targetProp = serializedObject.FindProperty("_target");
            var memberProp = serializedObject.FindProperty("_member");
            var formatProp = serializedObject.FindProperty("_format");

            var entry = DrawKeyField(config, keyProp);
            var selectedTarget = DrawComponentField(gameObject, binding, targetProp);
            DrawMemberField(selectedTarget, memberProp, formatProp, entry, keyProp.stringValue);

            serializedObject.ApplyModifiedProperties();
        }

        private static DSMEntryDefinition? DrawKeyField(DSMConfig? config, SerializedProperty keyProp)
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("No DSMConfig found in Resources. Falling back to a plain key field.", MessageType.Warning);
                EditorGUILayout.PropertyField(keyProp, new GUIContent("Key"));
                return null;
            }

            var keys = config.Entries.Select(e => e.Key).ToList();
            var currentKey = keyProp.stringValue;
            var options = new List<string>(keys.Count + 2) { string.Empty };
            var displayOptions = new List<GUIContent>(keys.Count + 2) { new("None") };

            if (currentKey.Length > 0 && !keys.Contains(currentKey))
            {
                options.Add(currentKey);
                displayOptions.Add(new GUIContent($"{currentKey} (missing)"));
            }

            foreach (var e in config.Entries)
            {
                options.Add(e.Key);
                displayOptions.Add(new GUIContent($"{e.Key} ({e.Type})"));
            }

            // "None" first, so an empty key doesn't display as the first entry while still being empty.
            var index = Mathf.Max(options.IndexOf(currentKey), 0);

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(new GUIContent("Key"), index, displayOptions.ToArray());
            if (EditorGUI.EndChangeCheck())
                keyProp.stringValue = options[newIndex];

            if (currentKey.Length > 0 && !keys.Contains(currentKey))
                EditorGUILayout.HelpBox($"Key '{currentKey}' is not defined in the DSM config.", MessageType.Warning);

            return config.TryGetEntry(keyProp.stringValue, out var entry) ? entry : null;
        }

        private Component? DrawComponentField(GameObject gameObject, DSMBinding binding, SerializedProperty targetProp)
        {
            var components = gameObject.GetComponents<Component>()
                .Where(c => c != null && c != binding)
                .ToList();

            var current = targetProp.objectReferenceValue as Component;

            if (components.Count == 0)
            {
                EditorGUILayout.HelpBox("No components found on this GameObject.", MessageType.Warning);
                return current;
            }

            // "None" first, for the same reason as the key popup.
            var options = DisambiguatedNames(components).Prepend("None").Select(n => new GUIContent(n)).ToArray();
            var index = current == null ? 0 : components.IndexOf(current) + 1;

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(new GUIContent("Component"), index, options);
            var changed = EditorGUI.EndChangeCheck();

            if (changed && newIndex >= 0 && newIndex <= components.Count)
            {
                var newTarget = newIndex == 0 ? null : components[newIndex - 1];
                if (newTarget != current)
                {
                    targetProp.objectReferenceValue = newTarget;
                    // A member id from the old component is unlikely to be valid on the new one; clear it here so
                    // DrawMemberField doesn't offer a stale selection while the two edits are applied together.
                    serializedObject.FindProperty("_member").stringValue = string.Empty;
                }
                return newTarget;
            }

            return current;
        }

        private static List<string> DisambiguatedNames(IReadOnlyList<Component> components)
        {
            var counts = new Dictionary<string, int>();
            var seen = new Dictionary<string, int>();
            foreach (var c in components)
            {
                var name = c.GetType().Name;
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }

            var result = new List<string>(components.Count);
            foreach (var c in components)
            {
                var name = c.GetType().Name;
                if (counts[name] <= 1)
                {
                    result.Add(name);
                    continue;
                }
                var n = seen.GetValueOrDefault(name) + 1;
                seen[name] = n;
                result.Add($"{name} ({n})");
            }
            return result;
        }

        private static readonly GUIContent FormatLabel =
            new("Format", ".NET composite format, {0} is the value. e.g. Speed: {0:0.0} m/s");

        private void DrawMemberField(Component? targetComponent, SerializedProperty memberProp, SerializedProperty formatProp,
            DSMEntryDefinition? entry, string key)
        {
            var enabled = entry != null && targetComponent != null;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (entry == null || targetComponent == null)
                {
                    EditorGUILayout.Popup(new GUIContent("Member"), 0, new[] { new GUIContent("None") });
                    return;
                }

                var valueType = DSMEntryDefinition.ClrTypeOf(entry.Type);
                var members = DSMBindingMembers.Find(targetComponent.GetType(), valueType);

                var ids = new List<string> { string.Empty };
                var displays = new List<GUIContent> { new("None") };

                var currentId = memberProp.stringValue;
                var currentValid = members.Any(m => m.id == currentId);
                if (currentId.Length > 0 && !currentValid)
                {
                    var name = DSMBindingMembers.DisplayNameOf(currentId) ?? currentId;
                    ids.Add(currentId);
                    displays.Add(new GUIContent($"{name} (missing)"));
                }

                foreach (var (id, display, _) in members)
                {
                    if (ids.Contains(id)) continue;
                    ids.Add(id);
                    displays.Add(new GUIContent(display));
                }

                var index = ids.IndexOf(currentId);
                if (index < 0) index = 0;

                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUILayout.Popup(new GUIContent("Member"), index, displays.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    memberProp.stringValue = ids[newIndex];
                    currentId = ids[newIndex];
                }

                if (currentId.Length > 0 && !members.Any(m => m.id == currentId))
                {
                    EditorGUILayout.HelpBox(
                        $"Member '{DSMBindingMembers.DisplayNameOf(currentId)}' is no longer valid: not found, or its type no longer matches '{valueType.Name}' or 'string'.",
                        MessageType.Warning);
                    return;
                }

                // Runtime prefers an exact-type match, so only a member with no exact match is formatted.
                var formatted = members.Any(m => m.id == currentId && m.formatted) &&
                                !members.Any(m => m.id == currentId && !m.formatted);
                if (formatted) DrawFormatField(formatProp, entry, key);
            }
        }

        private static void DrawFormatField(SerializedProperty formatProp, DSMEntryDefinition entry, string key)
        {
            EditorGUILayout.PropertyField(formatProp, FormatLabel);

            if (PreviewText(entry.Type, key, formatProp.stringValue, out var text, out var error))
                EditorGUILayout.LabelField("Preview", text);
            else
                EditorGUILayout.HelpBox($"Invalid format: {error}", MessageType.Error);
        }

        private static bool PreviewText(DSMDataType type, string key, string format, out string text, out string? error) =>
            type switch
            {
                DSMDataType.Int => DSMBindingMembers.TryFormat(format, DSM.Get<int>(key), out text, out error),
                DSMDataType.Float => DSMBindingMembers.TryFormat(format, DSM.Get<float>(key), out text, out error),
                DSMDataType.Bool => DSMBindingMembers.TryFormat(format, DSM.Get<bool>(key), out text, out error),
                DSMDataType.Vector2 => DSMBindingMembers.TryFormat(format, DSM.Get<Vector2>(key), out text, out error),
                DSMDataType.Vector3 => DSMBindingMembers.TryFormat(format, DSM.Get<Vector3>(key), out text, out error),
                DSMDataType.Color => DSMBindingMembers.TryFormat(format, DSM.Get<Color>(key), out text, out error),
                _ => DSMBindingMembers.TryFormat(format, DSM.Get<string>(key), out text, out error)
            };
    }
}
