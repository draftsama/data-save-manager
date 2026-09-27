#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace DataSaveManager.Tests
{
    /// <summary>Builds a <see cref="DSMConfig"/> for tests: AutoSave off, a unique temp save directory.</summary>
    internal static class TestConfigs
    {
        public static DSMConfig Create(params DSMEntryDefinition[] entries)
        {
            var config = ScriptableObject.CreateInstance<DSMConfig>();
            var dir = Path.Combine(Path.GetTempPath(), "dsm-tests", Guid.NewGuid().ToString("N"));
            config.SetForTests(false, 0f, dir, "save.json");
            foreach (var entry in entries)
                config.SetEntry(entry);
            return config;
        }
    }
}
