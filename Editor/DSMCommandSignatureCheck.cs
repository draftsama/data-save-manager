#nullable enable

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DataSaveManager.Editor
{
    /// <summary>Warns after every script reload about [DSMCommand] and [DSMEntryAction] methods that Register would skip, so the mistake shows before Play mode.</summary>
    [InitializeOnLoad]
    internal static class DSMCommandSignatureCheck
    {
        static DSMCommandSignatureCheck()
        {
            foreach (var method in TypeCache.GetMethodsWithAttribute<DSMCommandAttribute>())
            {
                var error = DSMCommandSignature.Validate(method);
                if (error != null && !IsTestAssembly(method.DeclaringType?.Assembly))
                    Debug.LogWarning($"DSM: [DSMCommand] {method.DeclaringType?.FullName}.{method.Name} will be skipped: {error}");
            }

            foreach (var method in TypeCache.GetMethodsWithAttribute<DSMEntryActionAttribute>())
            {
                var error = DSMEntryActionSignature.Validate(method, out _);
                if (error != null && !IsTestAssembly(method.DeclaringType?.Assembly))
                    Debug.LogWarning($"DSM: [DSMEntryAction] {method.DeclaringType?.FullName}.{method.Name} will be skipped: {error}");
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
