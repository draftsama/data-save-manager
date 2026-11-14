#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace OperatorConsole.Editor
{
    /// <summary>Draws a typed value field for a <see cref="OCDataType"/>, converting to/from <see cref="JToken"/> via the runtime serializer so it matches what the store persists.</summary>
    internal static class OCValueField
    {
        private static readonly OCSerializer Serializer = new();

        public static JToken DrawField(Rect rect, GUIContent label, OCDataType type, JToken? current, out bool changed)
        {
            var token = current ?? DefaultToken(type);

            switch (type)
            {
                case OCDataType.Int:
                {
                    var value = ToClr(token, 0);
                    var next = EditorGUI.IntField(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.Float:
                {
                    var value = ToClr(token, 0f);
                    var next = EditorGUI.FloatField(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.Bool:
                {
                    var value = ToClr(token, false);
                    var next = EditorGUI.Toggle(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.String:
                {
                    var value = ToClr(token, string.Empty);
                    var next = EditorGUI.TextField(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.Vector2:
                {
                    var value = ToClr(token, Vector2.zero);
                    var next = EditorGUI.Vector2Field(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.Vector3:
                {
                    var value = ToClr(token, Vector3.zero);
                    var next = EditorGUI.Vector3Field(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                case OCDataType.Color:
                {
                    var value = ToClr(token, Color.white);
                    var next = EditorGUI.ColorField(rect, label, value);
                    changed = next != value;
                    return ToJToken(next);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static JToken DefaultToken(OCDataType type)
        {
            try { return JToken.Parse(OCEntryDefinition.DefaultJsonFor(type)); }
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
