#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Encrypt-then-MAC save-file encryption: AES-256-CBC ciphertext plus an
/// HMAC-SHA256 tag computed over everything preceding the tag (magic + version +
/// salt + IV + ciphertext). The MAC is verified, in constant time, BEFORE any
/// AES decryption is attempted — a tampered, truncated, or wrong-key buffer is
/// rejected with <see cref="DSMEncryptionException"/> instead of ever producing
/// corrupted plaintext.
/// </summary>
public static class DSMEncryptor
{
    // Buffer layout: [MagicBytes(4)][FormatVersion(1)][salt(SaltSize)][IV(IvSize)][ciphertext][tag(MacSize)]
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("DSM2");
    private const byte FormatVersion = 2;
    private const int SaltSize = 32;
    private const int IvSize = 16;
    private const int MacSize = 32;
    private const int AesKeySize = 256; // bits
    private const int DerivedKeyBytes = (AesKeySize / 8) + MacSize; // AES key + HMAC key, from one KDF call

    // Iterations = 600000 follows current OWASP guidance for PBKDF2-HMAC-SHA256.
    // This is a documented assumption under Claude's Discretion (STATE.md flagged
    // PBKDF2 iteration counts as needing local benchmarking; research was disabled
    // this run). Benchmark on target hardware and do not drop below ~100000 — a
    // human may revisit this value.
    private const int Iterations = 600_000;

    private const int HeaderSize = 4 /*magic*/ + 1 /*version*/;
    private const int AesBlockSize = 16;
    private const int MinFramedLength = HeaderSize + SaltSize + IvSize + MacSize + AesBlockSize;

    public static byte[] Encrypt(string json, string password)
    {
        DSMEncryptionKey.Validate(password);

        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var (aesKey, macKey) = DeriveKeys(password, salt);

        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.Mode = CipherMode.CBC;
        aes.Key = aesKey;
        aes.GenerateIV();
        var iv = aes.IV;

        byte[] ciphertext;
        using (var encryptor = aes.CreateEncryptor())
        using (var ms = new MemoryStream())
        {
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                cs.Write(jsonBytes, 0, jsonBytes.Length);
                cs.FlushFinalBlock();
            }
            ciphertext = ms.ToArray();
        }

        using var prefix = new MemoryStream();
        prefix.Write(MagicBytes, 0, MagicBytes.Length);
        prefix.WriteByte(FormatVersion);
        prefix.Write(salt, 0, salt.Length);
        prefix.Write(iv, 0, iv.Length);
        prefix.Write(ciphertext, 0, ciphertext.Length);
        var prefixBytes = prefix.ToArray();

        using var hmac = new HMACSHA256(macKey);
        var tag = hmac.ComputeHash(prefixBytes);

        var result = new byte[prefixBytes.Length + tag.Length];
        Buffer.BlockCopy(prefixBytes, 0, result, 0, prefixBytes.Length);
        Buffer.BlockCopy(tag, 0, result, prefixBytes.Length, tag.Length);
        return result;
    }

    public static string Decrypt(byte[] data, string password)
    {
        DSMEncryptionKey.Validate(password);

        if (data.Length < MinFramedLength)
            throw new DSMEncryptionException(
                "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");

        for (var i = 0; i < MagicBytes.Length; i++)
        {
            if (data[i] != MagicBytes[i])
                throw new DSMEncryptionException(
                    "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");
        }

        if (data[MagicBytes.Length] != FormatVersion)
            throw new DSMEncryptionException(
                "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");

        var saltOffset = HeaderSize;
        var ivOffset = saltOffset + SaltSize;
        var ciphertextOffset = ivOffset + IvSize;
        var tagOffset = data.Length - MacSize;
        var ciphertextLength = tagOffset - ciphertextOffset;

        var salt = data[saltOffset..ivOffset];
        var iv = data[ivOffset..ciphertextOffset];

        var (aesKey, macKey) = DeriveKeys(password, salt);

        var prefixLength = tagOffset;
        using var hmac = new HMACSHA256(macKey);
        var expectedTag = hmac.ComputeHash(data, 0, prefixLength);
        var actualTag = data[tagOffset..];

        if (!CryptographicOperations.FixedTimeEquals(expectedTag, actualTag))
            throw new DSMEncryptionException(
                "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");

        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.Mode = CipherMode.CBC;
        aes.Key = aesKey;
        aes.IV = iv;

        try
        {
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(data, ciphertextOffset, ciphertextLength);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch (CryptographicException)
        {
            // MAC already verified above, so this should not happen — but never let
            // a low-level AES padding error escape as anything other than our own
            // integrity-verification exception.
            throw new DSMEncryptionException(
                "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");
        }
    }

    private static (byte[] AesKey, byte[] MacKey) DeriveKeys(string password, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var derived = kdf.GetBytes(DerivedKeyBytes);
        var aesKey = new byte[AesKeySize / 8];
        var macKey = new byte[MacSize];
        Buffer.BlockCopy(derived, 0, aesKey, 0, aesKey.Length);
        Buffer.BlockCopy(derived, aesKey.Length, macKey, 0, macKey.Length);
        return (aesKey, macKey);
    }
}

/// <summary>
/// Thrown by <see cref="DSMEncryptor.Decrypt"/> when an encrypted save buffer is
/// too short, has an unrecognized magic/version header, or fails MAC verification
/// (tamper, truncation, or wrong key). Messages are generic and never include the
/// key or any derived key/salt/IV bytes.
/// </summary>
public sealed class DSMEncryptionException : CryptographicException
{
    public DSMEncryptionException(string message) : base(message)
    {
    }

    public DSMEncryptionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
