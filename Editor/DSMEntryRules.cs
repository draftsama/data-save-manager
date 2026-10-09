#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>The Range a freshly enabled Range toggle starts with: 0-1 for Float, 0-100 for Int, widened so the parsed Default lies inside.</summary>
        public static (float min, float max) InitialRange(DSMDataType type, string defaultJson)
        {
            var isInt = type == DSMDataType.Int;
            var min = 0f;
            var max = isInt ? 100f : 1f;

            if (!float.TryParse(defaultJson, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
                return (min, max);

            if (value < min) min = isInt ? (float)Math.Floor(value) : value;
            if (value > max) max = isInt ? (float)Math.Ceiling(value) : value;
            return (min, max);
        }
    }
}
