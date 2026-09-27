#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DataSaveManager.Editor
{
    /// <summary>Pure validation rules for editing a <see cref="DSMConfig"/>'s entries, kept separate from the window for unit testing.</summary>
    internal static class DSMEntryRules
    {
        public static string? ValidateNewKey(DSMConfig config, string key)
        {
            var trimmed = key.Trim();
            if (trimmed.Length == 0) return "Key cannot be empty.";
            if (trimmed.Any(char.IsWhiteSpace)) return "Key cannot contain whitespace.";
            if (config.TryGetEntry(trimmed, out _)) return $"Key '{trimmed}' is already defined.";
            return null;
        }

        public static HashSet<string> DuplicateKeys(DSMConfig config) =>
            config.Entries
                .Select(e => e.Key)
                .GroupBy(k => k, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);
    }
}
