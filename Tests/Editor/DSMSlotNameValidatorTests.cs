#nullable enable

using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
public class DSMSlotNameValidatorTests
{
    [TestCase("../evil")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("..")]
    [TestCase("CON")]
    [TestCase("con")]
    [TestCase("LPT9")]
    [TestCase("nul")]
    [TestCase("")]
    [TestCase("has space")]
    [TestCase("has.dot")]
    public void Validate_WithInvalidName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => DSMSlotNameValidator.Validate(name));
    }

    [TestCase("default")]
    [TestCase("save_1")]
    [TestCase("slot-2")]
    [TestCase("Player01")]
    public void Validate_WithValidName_DoesNotThrow(string name)
    {
        Assert.DoesNotThrow(() => DSMSlotNameValidator.Validate(name));
    }

    [Test]
    public void Validate_WithNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DSMSlotNameValidator.Validate(null!));
    }

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void GetOrCreateSlot_WithInvalidName_ThrowsBeforeAnyFileAccess()
    {
        // Arrange — isolated save dir so the manager constructor's default-slot
        // Load() never touches the real Application.persistentDataPath.
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        // Act & Assert — GetSlot routes through GetOrCreateSlot
        Assert.Throws<ArgumentException>(() => manager.GetSlot("../evil"));
    }

    [Test]
    public void DeleteSlot_WithInvalidName_ThrowsBeforeAnyFileAccess()
    {
        // Arrange
        var config = DSMTestConfig.Create(savePath: _tempDir);
        var manager = new DSMSlotManager(config);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.DeleteSlot("a/../b"));
    }
}
