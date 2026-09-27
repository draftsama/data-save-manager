#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>Reflection over a component's public settable members of a given value type, shared by <see cref="DSMBinding"/> and its editor.</summary>
    internal static class DSMBindingMembers
    {
        private const string PropertyPrefix = "P:";
        private const string FieldPrefix = "F:";
        private const string MethodPrefix = "M:";

        /// <summary>
        /// Every public instance property/field/method on <paramref name="componentType"/> that can be set to
        /// <paramref name="valueType"/>, as (encoded id, display string, formatted). Exact-type matches come first
        /// with <c>formatted = false</c>; when <paramref name="valueType"/> isn't <see cref="string"/>, every
        /// <see cref="string"/>-settable member follows with <c>formatted = true</c> (bindable through a Format
        /// string — see <see cref="FormatValue{T}"/>), displayed as <c>DSM_SetX</c> so they read as DSM text setters.
        /// </summary>
        public static IReadOnlyList<(string id, string display, bool formatted)> Find(Type componentType, Type valueType)
        {
            var result = new List<(string id, string display, bool formatted)>();
            CollectMembers(componentType, valueType, formatted: false, result);
            if (valueType != typeof(string))
                CollectMembers(componentType, typeof(string), formatted: true, result);
            return result;
        }

        private static void CollectMembers(Type componentType, Type valueType, bool formatted, List<(string id, string display, bool formatted)> result)
        {

            foreach (var prop in componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != valueType) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                var setMethod = prop.GetSetMethod(nonPublic: false);
                if (setMethod == null) continue;
                if (formatted && IsUnityBaseMember(prop)) continue;
                result.Add(($"{PropertyPrefix}{prop.Name}",
                    formatted ? FormattedDisplay(prop.Name, false) : $"{prop.Name} ({DisplayTypeName(valueType)})", formatted));
            }

            foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != valueType) continue;
                if (field.IsInitOnly || field.IsLiteral) continue;
                if (formatted && IsUnityBaseMember(field)) continue;
                result.Add(($"{FieldPrefix}{field.Name}",
                    formatted ? FormattedDisplay(field.Name, false) : $"{field.Name} ({DisplayTypeName(valueType)})", formatted));
            }

            foreach (var method in componentType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsSpecialName) continue; // excludes property accessors (get_x/set_x)
                if (method.IsGenericMethodDefinition) continue;
                if (method.ReturnType != typeof(void)) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (parameters[0].ParameterType != valueType) continue;
                if (formatted && IsUnityBaseMember(method)) continue;
                // A SetText(string) method next to a text property would show as a second DSM_SetText; keep the property.
                if (formatted && result.Any(m => m.formatted && m.display.StartsWith($"DSM_{method.Name} ", StringComparison.Ordinal))) continue;
                result.Add(($"{MethodPrefix}{method.Name}",
                    formatted ? FormattedDisplay(method.Name, true) : $"{method.Name}({DisplayTypeName(valueType)})", formatted));
            }
        }

        // Unity's own string members on these bases (name, tag, CancelInvoke, SendMessage, ...) aren't meaningful
        // text targets, and listing them as DSM_ setters would be misleading.
        private static bool IsUnityBaseMember(MemberInfo member) =>
            member.DeclaringType == typeof(UnityEngine.Object) || member.DeclaringType == typeof(Component) ||
            member.DeclaringType == typeof(Behaviour) || member.DeclaringType == typeof(MonoBehaviour);

        private static string FormattedDisplay(string name, bool isMethod)
        {
            var setter = isMethod ? name : $"Set{char.ToUpperInvariant(name[0])}{name[1..]}";
            return $"DSM_{setter} (format \u2192 {(isMethod ? name + "(string)" : name)})";
        }

        /// <summary>Formats <paramref name="value"/> with a .NET composite format string (invariant culture). Throws <see cref="FormatException"/> on a bad format.</summary>
        public static string FormatValue<T>(string format, T value) =>
            string.Format(CultureInfo.InvariantCulture, string.IsNullOrEmpty(format) ? "{0}" : format, value);

        /// <summary>Non-throwing wrapper over <see cref="FormatValue{T}"/>.</summary>
        public static bool TryFormat<T>(string format, T value, out string result, out string? error)
        {
            try
            {
                result = FormatValue(format, value);
                error = null;
                return true;
            }
            catch (FormatException e)
            {
                result = string.Empty;
                error = e.Message;
                return false;
            }
        }

        /// <summary>True if <paramref name="id"/> resolves to a public, settable <see cref="string"/> member on <paramref name="componentType"/>.</summary>
        public static bool IsStringMember(Type componentType, string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2) return false;
            var name = id[2..];

            switch (id[..2])
            {
                case PropertyPrefix:
                {
                    var prop = componentType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    var setMethod = prop?.GetSetMethod(nonPublic: false);
                    return setMethod != null && prop!.PropertyType == typeof(string);
                }
                case MethodPrefix:
                {
                    var method = componentType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                    return method != null && method.ReturnType == typeof(void);
                }
                case FieldPrefix:
                {
                    var field = componentType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    return field != null && field.FieldType == typeof(string) && !field.IsInitOnly && !field.IsLiteral;
                }
                default:
                    return false;
            }
        }

        /// <summary>Resolves <paramref name="id"/> (as returned by <see cref="Find"/>) against <paramref name="target"/>'s actual type and builds a setter delegate for it.</summary>
        public static bool TryCreateSetter<T>(Component target, string id, out Action<T> setter)
        {
            setter = null!;
            if (string.IsNullOrEmpty(id) || id.Length < 2) return false;

            var name = id[2..];
            var type = target.GetType();

            switch (id[..2])
            {
                case PropertyPrefix:
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    var setMethod = prop?.GetSetMethod(nonPublic: false);
                    if (setMethod == null || prop!.PropertyType != typeof(T)) return false;
                    setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, setMethod);
                    return true;
                }
                case MethodPrefix:
                {
                    var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(T) }, null);
                    if (method == null || method.ReturnType != typeof(void)) return false;
                    setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, method);
                    return true;
                }
                case FieldPrefix:
                {
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || field.FieldType != typeof(T) || field.IsInitOnly || field.IsLiteral) return false;
                    setter = value => field.SetValue(target, value);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Re-derives a member id's display string against a live component type, for showing a saved-but-now-invalid selection.</summary>
        public static string? DisplayNameOf(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2) return null;
            return id[..2] switch
            {
                PropertyPrefix or FieldPrefix or MethodPrefix => id[2..],
                _ => null
            };
        }

        private static string DisplayTypeName(Type t) => t == typeof(int) ? "int"
            : t == typeof(float) ? "float"
            : t == typeof(bool) ? "bool"
            : t == typeof(string) ? "string"
            : t.Name;
    }
}
