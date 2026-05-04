#nullable enable

using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class DSMEncryptor
{
    private const int KeySize = 256;
    private const int IvSize = 16;
    private const int Iterations = 10000;

    // Fixed salt scoped to this project — do not change after shipping save files
    private static readonly byte[] s_salt = Encoding.UTF8.GetBytes("DSM_SALT_V1_UNITY");

    public static byte[] Encrypt(string json, string password)
    {
        using var aes = CreateAes(password, out var iv);
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();

        ms.Write(iv, 0, iv.Length);

        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        cs.Write(jsonBytes, 0, jsonBytes.Length);
        cs.FlushFinalBlock();

        return ms.ToArray();
    }

    public static string Decrypt(byte[] data, string password)
    {
        var iv = data[..IvSize];
        using var aes = CreateAesWithIv(password, iv);
        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(data, IvSize, data.Length - IvSize);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static Aes CreateAes(string password, out byte[] iv)
    {
        var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        using var deriveBytes = new Rfc2898DeriveBytes(password, s_salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(KeySize / 8);
        aes.GenerateIV();
        iv = aes.IV;
        return aes;
    }

    private static Aes CreateAesWithIv(string password, byte[] iv)
    {
        var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        using var deriveBytes = new Rfc2898DeriveBytes(password, s_salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(KeySize / 8);
        aes.IV = iv;
        return aes;
    }
}
