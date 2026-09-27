#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Draws a typed value field for a <see cref="DSMDataType"/>, converting to/from <see cref="JToken"/> via the runtime serializer so it matches what the store persists.</summary>
    internal static class DSMValueField
    {
        private static readonly DSMSerializer Serializer = new();

        public static JToken DrawField(GUIContent label, DSMDataType type, JToken? current, out bool changed)
        {
            var token = current ?? DefaultToken(type);

            switch (type)
            {
                case DSMDataType.Int:
                {
                    var value = ToClr(token, 0);
                    var next = EditorGUILayout.IntField(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.Float:
                {
                    var value = ToClr(token, 0f);
                    var next = EditorGUILayout.FloatField(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.Bool:
                {
                    var value = ToClr(token, false);
                    var next = EditorGUILayout.Toggle(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.String:
                {
                    var value = ToClr(token, string.Empty);
                    var next = EditorGUILayout.TextField(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.Vector2:
                {
                    var value = ToClr(token, Vector2.zero);
                    var next = EditorGUILayout.Vector2Field(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.Vector3:
                {
                    var value = ToClr(token, Vector3.zero);
                    var next = EditorGUILayout.Vector3Field(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case DSMDataType.Color:
                {
                    var value = ToClr(token, Color.white);
                    var next = EditorGUILayout.ColorField(label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static JToken DefaultToken(DSMDataType type)
        {
            try { return JToken.Parse(DSMEntryDefinition.DefaultJsonFor(type)); }
            catch { return JValue.CreateNull(); }
        }

        private static T ToClr<T>(JToken token, T fallback)
        {
            // Can't use `??` here: T is unconstrained, so the compiler can't assume it's nullable.
            try
            {
                var result = token.ToObject<T>(Serializer.JsonSerializer);
                return result is null ? fallback : result;
            }
            catch { return fallback; }
        }

        private static JToken ToJToken<T>(T value) => JToken.FromObject(value!, Serializer.JsonSerializer);
    }
}
