#nullable enable

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed class DSMMigrationRunner
{
    public static readonly DSMMigrationRunner Empty = new DSMMigrationRunner(System.Array.Empty<IDSMMigration>());

    private readonly IDSMMigration[] _steps;

    public int CurrentVersion { get; }

    public DSMMigrationRunner(IEnumerable<IDSMMigration> migrations)
    {
        _steps = migrations.OrderBy(m => m.FromVersion).ToArray();

        for (var i = 0; i < _steps.Length; i++)
        {
            var expected = i + 1;
            if (_steps[i].FromVersion != expected)
                throw new DSMSaveVersionException(
                    $"DSM: migration chain is not contiguous — expected a step FromVersion {expected} but found {_steps[i].FromVersion}.");
        }

        CurrentVersion = _steps.Length == 0 ? 1 : _steps[^1].FromVersion + 1;
    }

    public int Migrate(JObject data, int fromVersion)
    {
        if (fromVersion < 1) fromVersion = 1;
        if (fromVersion == CurrentVersion) return CurrentVersion;
        if (fromVersion > CurrentVersion)
            throw new DSMSaveVersionException(
                $"DSM: save version {fromVersion} is newer than the current version {CurrentVersion} — refusing to load.");

        // Safe to apply every step with FromVersion >= fromVersion in order: the constructor
        // validated the chain is contiguous 1..CurrentVersion-1, so these steps always bridge
        // fromVersion..CurrentVersion with no missing link.
        foreach (var step in _steps.Where(s => s.FromVersion >= fromVersion))
            step.Migrate(data);

        return CurrentVersion;
    }
}
