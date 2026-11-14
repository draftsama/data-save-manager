#nullable enable

using System.Collections.Generic;

namespace DataSaveManager
{
    /// <summary>The optional parts of a Command registered from code instead of with <see cref="DSMCommandAttribute"/>.</summary>
    public sealed class DSMCommandOptions
    {
        public string? Description { get; set; }
        public bool Confirm { get; set; }

        /// <summary>Audience names, as declared in the Config. None: every Runtime Panel shows the Command.</summary>
        public IReadOnlyList<string>? Audiences { get; set; }

        public string? Header { get; set; }
        public int Order { get; set; }
    }
}
