#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace OperatorConsole.Editor
{
    /// <summary>What the Manager window's tabs share: the Config being edited and the window-wide filters.</summary>
    internal sealed class OCManagerContext
    {
        public OCManagerContext(OCConfig config, SerializedObject so)
        {
            Config = config;
            So = so;
        }

        public OCConfig Config { get; }
        public SerializedObject So { get; }
        public string[] AudienceNames { get; set; } = Array.Empty<string>();
        public HashSet<string> DuplicateKeys { get; set; } = new();
        public int ViewAsIndex { get; set; } = -1;
        public string Search { get; set; } = string.Empty;

        public bool ValuesEditable => OC.Store.Config == Config;
        public SerializedProperty Entries => So.FindProperty("_entries");

        /// <summary>The picked View as Audience's id, or null when View as is All.</summary>
        public string[]? ViewAsIds() =>
            ViewAsIndex >= 0 && ViewAsIndex < Config.Audiences.Count ? new[] { Config.Audiences[ViewAsIndex].Id } : null;
    }
}
