#nullable enable

using NUnit.Framework;

[TestFixture]
public class DSMEncryptionKeyTests
{
    [Test]
    public void Validate_NullKey_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(() => DSMEncryptionKey.Validate(null));
    }

    [Test]
    public void Validate_EmptyKey_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(() => DSMEncryptionKey.Validate(""));
    }

    [Test]
    public void Validate_WhitespaceKey_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(() => DSMEncryptionKey.Validate("        "));
    }

    [Test]
    public void Validate_TooShortKey_ThrowsArgumentException()
    {
        var tooShort = new string('a', DSMEncryptionKey.MinLength - 1);
        Assert.Throws<System.ArgumentException>(() => DSMEncryptionKey.Validate(tooShort));
    }

    [Test]
    public void Validate_ValidKey_DoesNotThrow()
    {
        var validKey = new string('a', DSMEncryptionKey.MinLength);
        Assert.DoesNotThrow(() => DSMEncryptionKey.Validate(validKey));
    }

    [Test]
    public void Validate_ExceptionMessage_DoesNotLeakKeyValue()
    {
        const string secretKey = "sh0rt-but-secret";
        var tooShort = secretKey.Substring(0, DSMEncryptionKey.MinLength - 1);
        var ex = Assert.Throws<System.ArgumentException>(() => DSMEncryptionKey.Validate(tooShort));
        Assert.That(ex!.Message, Does.Not.Contain(tooShort));
    }
}
