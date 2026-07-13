#nullable enable

using System;
using System.Text.RegularExpressions;

/// <summary>
/// Validates slot names before they are used to build file paths or dictionary
/// keys in <see cref="DSMSlotManager"/>. Rejects path traversal, path
/// separators, and Windows-reserved device names (BUGS-03/D-03).
/// </summary>
public static class DSMSlotNameValidator
{
    private static readonly Regex ValidPattern = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="name"/> is null/empty,
    /// contains anything outside [A-Za-z0-9_-], contains a "..") traversal segment, or
    /// matches a Windows-reserved device name (case-insensitive).
    /// </summary>
    public static void Validate(string name)
    {
        if (string.IsNullOrEmpty(name) || !ValidPattern.IsMatch(name) || name.Contains(".."))
            throw new ArgumentException($"DSM: Invalid slot name '{name}'.", nameof(name));

        foreach (var reserved in ReservedNames)
            if (string.Equals(name, reserved, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"DSM: Slot name '{name}' is a reserved device name.", nameof(name));
    }
}
