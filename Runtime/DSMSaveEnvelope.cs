#nullable enable

using System;
using Newtonsoft.Json.Linq;

public static class DSMSaveEnvelope
{
    public const string VersionKey = "version";
    public const string DataKey = "data";
    public const int LegacyVersion = 1;

    public static JObject Wrap(JObject data, int version) =>
        new JObject { [VersionKey] = version, [DataKey] = data };

    public static bool TryUnwrap(JObject root, out int version, out JObject data)
    {
        if (root[VersionKey] is { Type: JTokenType.Integer } versionToken && root[DataKey] is JObject dataObj)
        {
            version = (int)versionToken;
            data = dataObj;
            return true;
        }

        version = LegacyVersion;
        data = root;
        return false;
    }
}

public sealed class DSMSaveVersionException : Exception
{
    public DSMSaveVersionException(string message) : base(message)
    {
    }
}
