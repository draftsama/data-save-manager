#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OperatorConsole
{
    internal enum OCMemberMatch
    {
        None,
        Exact,
        Formatted
    }

    /// <summary>Reflection over a component's public settable members of a given value type, shared by <see cref="OCBinding"/> and its editor.</summary>
    internal static class OCBindingMembers
    {
        private const string PropertyPrefix = "P:";
        private const string FieldPrefix = "F:";
        private const string MethodPrefix = "M:";

        /// <summary>
        /// Every public instance property/field/method on <paramref name="componentType"/> that can be set to
        /// <paramref name="valueType"/>, as (encoded id, display string, formatted). Exact-type matches come first
        /// with <c>formatted = false</c>; when <paramref name="valueType"/> isn't <see cref="string"/>, every
        /// <see cref="string"/>-settable member follows with <c>formatted = true</c> (bindable through a Format
        /// string — see <see cref="FormatValue{T}"/>), displayed as <c>OC_SetX</c> so they read as OC text setters.
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
                // A SetText(string) method next to a text property would show as a second OC_SetText; keep the property.
                if (formatted && result.Any(m => m.formatted && m.display.StartsWith($"OC_{method.Name} ", StringComparison.Ordinal))) continue;
                result.Add(($"{MethodPrefix}{method.Name}",
                    formatted ? FormattedDisplay(method.Name, true) : $"{method.Name}({DisplayTypeName(valueType)})", formatted));
            }
        }

        // Unity's own string members on these bases (name, tag, CancelInvoke, SendMessage, ...) aren't meaningful
        // text targets, and listing them as OC_ setters would be misleading.
        private static bool IsUnityBaseMember(MemberInfo member) =>
            member.DeclaringType == typeof(UnityEngine.Object) || member.DeclaringType == typeof(Component) ||
            member.DeclaringType == typeof(Behaviour) || member.DeclaringType == typeof(MonoBehaviour);

        private static string FormattedDisplay(string name, bool isMethod)
        {
            var setter = isMethod ? name : $"Set{char.ToUpperInvariant(name[0])}{name[1..]}";
            return $"OC_{setter} (format \u2192 {(isMethod ? name + "(string)" : name)})";
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

        /// <summary>How a Link's member resolves for <paramref name="valueType"/>: an exact-type member wins; otherwise a text member (not one of Unity's base members) takes the Value through the Link's Format.</summary>
        public static OCMemberMatch Match(Type componentType, string id, Type valueType)
        {
            if (FindSettable(componentType, id, valueType) != null) return OCMemberMatch.Exact;
            if (valueType == typeof(string)) return OCMemberMatch.None;
            var text = FindSettable(componentType, id, typeof(string));
            return text != null && !IsUnityBaseMember(text) ? OCMemberMatch.Formatted : OCMemberMatch.None;
        }

        /// <summary>
        /// Builds the setter a Link drives, following <see cref="Match"/>. A formatted setter passes the Value through
        /// <paramref name="format"/>; on a bad format it writes the Value's <c>ToString()</c> instead and calls
        /// <paramref name="onFormatError"/> with the error, at most once per setter.
        /// </summary>
        public static bool TryCreateSetter<T>(Component target, string id, string format, Action<string>? onFormatError, out Action<T> setter)
        {
            switch (Match(target.GetType(), id, typeof(T)))
            {
                case OCMemberMatch.Exact:
                    return TryCreateExactSetter(target, id, out setter);

                case OCMemberMatch.Formatted when TryCreateExactSetter<string>(target, id, out var setText):
                    var reported = false;
                    setter = value =>
                    {
                        if (!TryFormat(format, value, out var text, out var error))
                        {
                            if (!reported)
                            {
                                reported = true;
                                onFormatError?.Invoke(error!);
                            }
                            text = value?.ToString() ?? string.Empty;
                        }
                        setText(text);
                    };
                    return true;

                default:
                    setter = null!;
                    return false;
            }
        }

        /// <summary>The member the IL2CPP linker must keep for <paramref name="id"/>: a property's setter (<c>set_X</c>), a field, or a method.</summary>
        public static bool TryGetPreservedMember(string id, out string memberName, out bool isField)
        {
            memberName = string.Empty;
            isField = false;
            if (string.IsNullOrEmpty(id) || id.Length < 3) return false;

            var name = id[2..];
            switch (id[..2])
            {
                case PropertyPrefix: memberName = $"set_{name}"; return true;
                case FieldPrefix: memberName = name; isField = true; return true;
                case MethodPrefix: memberName = name; return true;
                default: return false;
            }
        }

        private static bool TryCreateExactSetter<T>(Component target, string id, out Action<T> setter)
        {
            switch (FindSettable(target.GetType(), id, typeof(T)))
            {
                case MethodInfo method:
                    setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, method);
                    return true;
                case FieldInfo field:
                    setter = value => field.SetValue(target, value);
                    return true;
                default:
                    setter = null!;
                    return false;
            }
        }

        // A property resolves to its public setter, so every result is a MethodInfo or a FieldInfo.
        private static MemberInfo? FindSettable(Type type, string id, Type memberType)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2) return null;
            var name = id[2..];

            switch (id[..2])
            {
                case PropertyPrefix:
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    var setMethod = prop?.GetSetMethod(nonPublic: false);
                    return setMethod != null && prop!.PropertyType == memberType ? setMethod : null;
                }
                case MethodPrefix:
                {
                    var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { memberType }, null);
                    return method != null && method.ReturnType == typeof(void) ? method : null;
                }
                case FieldPrefix:
                {
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    return field != null && field.FieldType == memberType && !field.IsInitOnly && !field.IsLiteral ? field : null;
                }
                default:
                    return null;
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
