#nullable enable

using System;
using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>Decides which Exposed Entries and Commands a Runtime Panel shows, from the Audiences each names.</summary>
    public static class OCAudienceFilter
    {
        /// <summary>True when <paramref name="entry"/> is Exposed and either side names no Audience, or both share at least one.</summary>
        public static bool Shows(OCEntryDefinition entry, IReadOnlyList<string> panelAudienceIds)
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

        /// <summary>True when either side names no Audience, or both share at least one. The Command names Audiences by name, resolved through <paramref name="config"/>; an unknown name matches nothing.</summary>
        public static bool Shows(OCRegisteredCommand command, IReadOnlyList<string> panelAudienceIds, OCConfig config)
        {
            var names = command.AudienceNames;
            if (panelAudienceIds.Count == 0 || names.Count == 0) return true;

            foreach (var name in names)
            {
                if (!config.TryGetAudienceByName(name, out var audience)) continue;
                for (var i = 0; i < panelAudienceIds.Count; i++)
                    if (string.Equals(panelAudienceIds[i], audience.Id, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
