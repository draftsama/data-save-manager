#nullable enable

using System.IO;
using UnityEngine;

public static class DSMPaths
{
    public static string GetSaveDirectory(string? configPath) =>
        string.IsNullOrEmpty(configPath)
            ? Path.Combine(Application.persistentDataPath, "DSM")
            : configPath;
}
