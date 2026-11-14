#nullable enable

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OperatorConsole.Editor
{
    /// <summary>Warns after every script reload about [OCCommand] and [OCEntryAction] methods that Register would skip, so the mistake shows before Play mode.</summary>
    [InitializeOnLoad]
    internal static class OCCommandSignatureCheck
    {
        static OCCommandSignatureCheck()
        {
            foreach (var method in TypeCache.GetMethodsWithAttribute<OCCommandAttribute>())
            {
                var error = OCCommandSignature.Validate(method);
                if (error != null && !IsTestAssembly(method.DeclaringType?.Assembly))
                    Debug.LogWarning($"OC: [OCCommand] {method.DeclaringType?.FullName}.{method.Name} will be skipped: {error}");
            }

            foreach (var method in TypeCache.GetMethodsWithAttribute<OCEntryActionAttribute>())
            {
                var error = OCEntryActionSignature.Validate(method, out _);
                if (error != null && !IsTestAssembly(method.DeclaringType?.Assembly))
                    Debug.LogWarning($"OC: [OCEntryAction] {method.DeclaringType?.FullName}.{method.Name} will be skipped: {error}");
            }
        }

        // Test assemblies declare invalid Commands on purpose, to test that Register skips them.
        private static bool IsTestAssembly(Assembly? assembly)
        {
            if (assembly == null) return false;
            foreach (var reference in assembly.GetReferencedAssemblies())
                if (string.Equals(reference.Name, "nunit.framework", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
