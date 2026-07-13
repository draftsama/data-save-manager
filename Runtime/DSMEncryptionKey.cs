#nullable enable

using System;

/// <summary>
/// Single shared accessor for validating a DSM encryption key. Both the runtime
/// path (<see cref="DSMEncryptor"/>, <see cref="DSMConfig.SetEncryptionKey"/>) and
/// Editor tooling (DSMConfigEditor, DSMManagerWindow) must call <see cref="Validate"/>
/// before using a key so that empty/short keys are rejected consistently everywhere.
/// </summary>
public static class DSMEncryptionKey
{
    // MinLength = 8 is a documented default under Claude's Discretion: a defensible
    // floor for a developer-set key, not a cryptographic strength guarantee. A
    // developer may require a longer key for their own threat model.
    public const int MinLength = 8;

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="key"/> is null, empty,
    /// whitespace-only, or shorter than <see cref="MinLength"/>. The exception message
    /// describes the rule only — it never echoes the key value.
    /// </summary>
    public static void Validate(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinLength)
            throw new ArgumentException(
                $"DSM: encryption key must be non-empty and at least {MinLength} characters long.",
                nameof(key));
    }
}
