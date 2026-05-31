#nullable enable

using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class DSMEncryptor
{
    private const int KeySize = 256;
    private const int IvSize = 16;
    private const int SaltSize = 32;
    private const int Iterations = 10000;

    // File format: [16-byte IV][32-byte salt][ciphertext]
    // Salt is generated randomly per Encrypt() call — no fixed salt.

    public static byte[] Encrypt(string json, string password)
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        using var aes = CreateAes(password, salt, out var iv);
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length);
        ms.Write(salt, 0, salt.Length);
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        cs.Write(jsonBytes, 0, jsonBytes.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    public static string Decrypt(byte[] data, string password)
    {
        var iv   = data[..IvSize];
        var salt = data[IvSize..(IvSize + SaltSize)];
        using var aes = CreateAesWithIv(password, salt, iv);
        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(data, IvSize + SaltSize, data.Length - IvSize - SaltSize);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static Aes CreateAes(string password, byte[] salt, out byte[] iv)
    {
        var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(KeySize / 8);
        aes.GenerateIV();
        iv = aes.IV;
        return aes;
    }

    private static Aes CreateAesWithIv(string password, byte[] salt, byte[] iv)
    {
        var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(KeySize / 8);
        aes.IV = iv;
        return aes;
    }
}
