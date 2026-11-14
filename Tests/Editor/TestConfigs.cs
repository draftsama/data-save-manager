#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace OperatorConsole.Tests
{
    /// <summary>Builds a <see cref="OCConfig"/> for tests: AutoSave off, a unique temp save directory.</summary>
    internal static class TestConfigs
    {
        public static OCConfig Create(params OCEntryDefinition[] entries)
        {
            var config = ScriptableObject.CreateInstance<OCConfig>();
            var dir = Path.Combine(Path.GetTempPath(), "dsm-tests", Guid.NewGuid().ToString("N"));
            config.SetForTests(false, 0f, dir, "save.json");
            foreach (var entry in entries)
                config.SetEntry(entry);
            return config;
        }
    }
}
