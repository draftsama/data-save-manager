#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace OperatorConsole.Editor
{
    /// <summary>Pure validation rules for the Keys of a <see cref="OCConfig"/>'s entries, kept separate from the window for unit testing. Presentation Hint rules live in <see cref="OCPresentation"/>.</summary>
    internal static class OCEntryRules
    {
        public static string? ValidateNewKey(OCConfig config, string key)
        {
            var trimmed = key.Trim();
            if (trimmed.Length == 0) return "Key cannot be empty.";
            if (trimmed.Any(char.IsWhiteSpace)) return "Key cannot contain whitespace.";
            if (config.TryGetEntry(trimmed, out _)) return $"Key '{trimmed}' is already defined.";
            return null;
        }

        /// <summary>Keys of the Entries that name <paramref name="audienceId"/>, in Config order.</summary>
        public static List<string> EntriesNamingAudience(OCConfig config, string audienceId) =>
            config.Entries
                .Where(e => e.AudienceIds.Contains(audienceId, StringComparer.Ordinal))
                .Select(e => e.Key)
                .ToList();

        public static HashSet<string> DuplicateKeys(OCConfig config) =>
            config.Entries
                .Select(e => e.Key)
                .GroupBy(k => k, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);
    }
}
