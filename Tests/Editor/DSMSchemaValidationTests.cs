#nullable enable

using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
public class DSMSchemaValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_SchemaTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Strict_SetTypeMismatch_ThrowsSchemaViolation()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-set");

        Assert.Throws<DSMSchemaViolationException>(() => slot.Set("testKey", "not-an-int"));
    }

    [Test]
    public void Strict_GetTypeMismatch_ThrowsSchemaViolation()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-get");

        Assert.Throws<DSMSchemaViolationException>(() => slot.Get("testKey", "wrong-default-type"));
    }

    [Test]
    public void Strict_MatchingType_DoesNotThrow()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-match");

        Assert.DoesNotThrow(() => slot.Set("testKey", 5));
        Assert.DoesNotThrow(() => slot.Get("testKey", 0));
    }

    [Test]
    public void Strict_UnconstrainedKey_PassesThrough()
    {
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("strict-unconstrained");

        Assert.DoesNotThrow(() => slot.Set("freeform", "anything"));
    }

    [Test]
    public void Lenient_Mismatch_CoercesToSchemaType()
    {
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-coerce");

        Assert.DoesNotThrow(() => slot.Set("testKey", "42"));
        Assert.That(slot.Get("testKey", 0), Is.EqualTo(42));
    }

    [Test]
    public void Lenient_UncoercibleMismatch_DoesNotThrow()
    {
        var config = DSMTestConfig.Create(strictSchema: false, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("lenient-uncoercible");

        Assert.DoesNotThrow(() => slot.Set("testKey", "not-a-number"));
    }

    [Test]
    public void Strict_ExceptionMessage_DoesNotLeakOffendingValue()
    {
        const string secretValue = "s3cr3t-value";
        var config = DSMTestConfig.Create(strictSchema: true, savePath: _tempDir);
        var manager = new DSMSlotManager(config);
        var slot = manager.GetSlot("leak-check");

        var ex = Assert.Throws<DSMSchemaViolationException>(() => slot.Set("testKey", secretValue));
        Assert.That(ex!.Message, Does.Not.Contain(secretValue));
    }

    [Test]
    public void EmptySchema_NullConstantType_IsPassThroughNoOp()
    {
        var schema = DSMSchema.For(null);

        Assert.That(schema.Count, Is.EqualTo(0));
        Assert.That(schema.TryGetExpectedType("testKey", out _), Is.False);
    }
}
