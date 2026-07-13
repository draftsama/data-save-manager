#nullable enable

using NUnit.Framework;

[TestFixture]
public class DSMEncryptorTests
{
    private const string ValidKey = "correct-horse-battery-staple";
    private const string DifferentValidKey = "another-valid-key-that-differs";
    private const string SampleJson = "{\"hp\":42,\"name\":\"hero\"}";

    [Test]
    public void EncryptThenDecrypt_ValidKey_RoundTripsOriginalJson()
    {
        var encrypted = DSMEncryptor.Encrypt(SampleJson, ValidKey);
        var decrypted = DSMEncryptor.Decrypt(encrypted, ValidKey);

        Assert.That(decrypted, Is.EqualTo(SampleJson));
    }

    [Test]
    public void Decrypt_TamperedCiphertext_ThrowsDSMEncryptionException()
    {
        var encrypted = DSMEncryptor.Encrypt(SampleJson, ValidKey);

        // Flip one byte near the end of the buffer (inside ciphertext, before the trailing MAC tag).
        var tampered = (byte[])encrypted.Clone();
        var flipIndex = tampered.Length - 40; // within ciphertext region, not the 32-byte tag
        tampered[flipIndex] ^= 0xFF;

        Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(tampered, ValidKey));
    }

    [Test]
    public void Decrypt_TruncatedBuffer_ThrowsDSMEncryptionException()
    {
        var encrypted = DSMEncryptor.Encrypt(SampleJson, ValidKey);

        var truncated = new byte[encrypted.Length - 8];
        System.Array.Copy(encrypted, truncated, truncated.Length);

        Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(truncated, ValidKey));
    }

    [Test]
    public void Decrypt_WrongKey_ThrowsDSMEncryptionException()
    {
        var encrypted = DSMEncryptor.Encrypt(SampleJson, ValidKey);

        Assert.Throws<DSMEncryptionException>(() => DSMEncryptor.Decrypt(encrypted, DifferentValidKey));
    }

    [Test]
    public void Encrypt_EmptyKey_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(() => DSMEncryptor.Encrypt(SampleJson, ""));
    }
}
