#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class DSMSlotDebounceTests
{
    private DSMConfig _config = null!;
    private DSMSerializer _serializer = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _config = DSMTestConfig.Create(autoSave: true, autoSaveDebounce: 0.05f);
        _serializer = new DSMSerializer();
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
    public async Task RapidSetStorm_NeverThrowsDisposedCts()
    {
        // Arrange — AutoSave enabled with a short debounce, per D-01/CONC-02
        var slot = new DSMSlot("storm", _config, _serializer, _tempDir, null);

        // Act — fire Set() rapidly in a tight loop; each call reschedules the debounce
        Assert.DoesNotThrow(() =>
        {
            for (var i = 0; i < 200; i++)
                slot.Set("counter", i);
        });

        // Let the debounce loop settle and fire its save so any deferred exception
        // (e.g. ObjectDisposedException surfacing asynchronously) has a chance to appear.
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task RapidSetStorm_PersistsLastValue()
    {
        // Arrange
        var slot = new DSMSlot("storm-value", _config, _serializer, _tempDir, null);

        // Act — Set() storm, last value must be the one persisted once the debounce settles
        for (var i = 0; i < 200; i++)
            slot.Set("counter", i);

        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert
        var destPath = Path.Combine(_tempDir, "storm-value.json");
        Assert.That(File.Exists(destPath), Is.True, "debounced save should have fired after settling");
        var root = JObject.Parse(File.ReadAllText(destPath));
        Assert.That((int)root["counter"]!, Is.EqualTo(199));
    }
}
