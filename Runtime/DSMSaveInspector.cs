#nullable enable

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

public static class DSMSaveInspector
{
    /// <summary>
    /// Reads the save version recorded in a slot file, decrypting first when the file is
    /// encrypted. Returns false with <see cref="DSMSaveEnvelope.LegacyVersion"/> when the file
    /// is missing, undecryptable, or unparseable; a readable file with no envelope is a
    /// legacy save and reports true with <see cref="DSMSaveEnvelope.LegacyVersion"/>.
    /// </summary>
    // Read-only by contract: no migration and no write-back, so showing a slot's version in
    // a tool can never mutate the save it is reporting on.
    public static bool TryReadOnDiskVersion(string slotFilePath, DSMConfig config, out int version)
    {
        version = DSMSaveEnvelope.LegacyVersion;
        if (string.IsNullOrEmpty(slotFilePath) || !File.Exists(slotFilePath)) return false;

        try
        {
            var json = IsEncrypted(slotFilePath, config)
                ? DSMEncryptor.Decrypt(File.ReadAllBytes(slotFilePath), config.EncryptionKey)
                : File.ReadAllText(slotFilePath, Encoding.UTF8);

            DSMSaveEnvelope.TryUnwrap(JObject.Parse(json), out var onDisk, out _);
            version = onDisk;
            return true;
        }
        catch (Exception)
        {
            // The false return IS the error signal — callers render "unknown" rather than
            // having an exception escape into an Editor render loop.
            version = DSMSaveEnvelope.LegacyVersion;
            return false;
        }
    }

    private static bool IsEncrypted(string slotFilePath, DSMConfig config)
    {
        var ext = Path.GetExtension(slotFilePath);
        if (ext.Equals(".enc", StringComparison.OrdinalIgnoreCase)) return true;
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)) return false;
        return config.Encrypt;
    }
}
