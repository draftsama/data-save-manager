#nullable enable
#if UNITY_EDITOR

using UnityEditor;

/// <summary>
/// After every compile, refreshes the DSM Manager window so it re-reads
/// DSMConstant via reflection and shows any newly added constants.
/// (Unity already calls OnEnable on open windows after domain reload,
///  so this is a safety net for edge cases like docked/minimised windows.)
/// </summary>
[InitializeOnLoad]
public static class DSMConstantSyncer
{
    static DSMConstantSyncer()
    {
        AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
    }

    private static void OnAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorWindow.HasOpenInstances<DSMManagerWindow>())
                EditorWindow.GetWindow<DSMManagerWindow>(false, null, false)?.Reload();
        };
    }
}

#endif
