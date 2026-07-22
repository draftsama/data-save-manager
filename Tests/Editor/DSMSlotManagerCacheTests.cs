#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class DSMSlotManagerCacheTests
{
    private DSMConfig _config = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_SlotCache_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _config = DSMTestConfig.Create(savePath: _tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void GetAllSlots_AfterCreatingSlotThroughManager_IncludesNewSlot()
    {
        var manager = new DSMSlotManager(_config);
        var slot = manager.GetSlot("alpha");
        slot.Set("hp", 1);
        slot.Save();

        Assert.That(manager.GetAllSlots(), Has.Member("alpha"));
    }

    // This case and GetAllSlots_FirstCallOnFreshManager_SeesPreExistingSaveFile are the
    // PERF-03 caching contract: a cache that never invalidates passes everything else.
    [Test]
    public void GetAllSlots_AfterDeleteSlot_DropsDeletedSlot()
    {
        var manager = new DSMSlotManager(_config);
        var slot = manager.GetSlot("alpha");
        slot.Set("hp", 1);
        slot.Save();
        Assert.That(manager.GetAllSlots(), Has.Member("alpha"));

        manager.DeleteSlot("alpha");

        Assert.That(manager.GetAllSlots(), Has.No.Member("alpha"));
    }

    [Test]
    public void GetAllSlots_FirstCallOnFreshManager_SeesPreExistingSaveFile()
    {
        var seeder = new DSMSlotManager(_config);
        File.WriteAllText(Path.Combine(seeder.SaveDirectory, "foo.json"), "{}");

        var manager = new DSMSlotManager(_config);

        Assert.That(manager.GetAllSlots(), Has.Member("foo"));
    }

    [Test]
    public void GetAllSlots_CalledTwiceWithoutChanges_ReturnsSameSet()
    {
        var manager = new DSMSlotManager(_config);
        var slot = manager.GetSlot("alpha");
        slot.Set("hp", 1);
        slot.Save();

        var first = manager.GetAllSlots().ToArray();
        var second = manager.GetAllSlots().ToArray();

        CollectionAssert.AreEquivalent(first, second);
    }

    [Test]
    public void GetAllSlots_AfterReadOnlyAccessToExistingSlot_KeepsSameSet()
    {
        var manager = new DSMSlotManager(_config);
        var slot = manager.GetSlot("alpha");
        slot.Set("hp", 1);
        slot.Save();
        var before = manager.GetAllSlots().ToArray();

        manager.GetSlot("alpha");
        manager.UseSlot("alpha");

        CollectionAssert.AreEquivalent(before, manager.GetAllSlots());
    }
}
