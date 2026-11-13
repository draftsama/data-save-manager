#nullable enable

using System;
using System.Collections.Generic;

namespace DataSaveManager
{
    /// <summary>Decides which Exposed Entries a Runtime Panel shows, from the Audiences each names.</summary>
    public static class DSMAudienceFilter
    {
        /// <summary>True when <paramref name="entry"/> is Exposed and either side names no Audience, or both share at least one.</summary>
        public static bool Shows(DSMEntryDefinition entry, IReadOnlyList<string> panelAudienceIds)
        {
            if (!entry.Exposed) return false;

            var entryAudienceIds = entry.AudienceIds;
            if (panelAudienceIds.Count == 0 || entryAudienceIds.Count == 0) return true;

            foreach (var id in entryAudienceIds)
            {
                for (var i = 0; i < panelAudienceIds.Count; i++)
                    if (string.Equals(panelAudienceIds[i], id, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
