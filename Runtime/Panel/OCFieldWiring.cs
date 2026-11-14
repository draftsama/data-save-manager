#nullable enable

using System;
using System.Reflection;

namespace OperatorConsole
{
    /// <summary>Assigns the private UI references of components that <see cref="OCPanelFactory"/> builds in code.</summary>
    internal static class OCFieldWiring
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        /// <exception cref="MissingFieldException">No field named <paramref name="fieldName"/> on the target's type or its base types.</exception>
        public static void Set(object target, string fieldName, object? value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, Flags);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, fieldName);
        }
    }
}
