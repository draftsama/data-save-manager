#nullable enable

using System.IO;
using UnityEngine;

namespace DataSaveManager
{
    public static class DSMPaths
    {
        public static string ResolveSaveFilePath(DSMConfig config) =>
            Resolve(config.SaveDirectory, config.FileName, Path.GetDirectoryName(Application.dataPath) ?? string.Empty, Application.persistentDataPath);

        internal static string Resolve(string saveDirectory, string fileName, string appRoot, string persistentDataPath)
        {
            string dir;
            if (string.IsNullOrEmpty(saveDirectory))
                dir = Path.Combine(persistentDataPath, "DSM");
            else if (Path.IsPathRooted(saveDirectory))
                dir = saveDirectory;
            else
                dir = Path.GetFullPath(Path.Combine(appRoot, saveDirectory));

            var file = string.IsNullOrEmpty(fileName) ? "save.json" : fileName;
            return Path.Combine(dir, file);
        }
    }
}
