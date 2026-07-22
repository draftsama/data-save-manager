#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class DSMManagerWindowStateTests
{
    private const string SlotA = "alpha";
    private const string SlotB = "beta";
    private const string KeyOnlyInA = "alpha-only";
    private const string KeyOnlyInB = "beta-only";
    private const int CurrentVersion = 1;

    private DSMConfig _config = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_WindowState_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _config = DSMTestConfig.Create(savePath: _tempDir);

        var manager = new DSMSlotManager(_config);
        SeedSlot(manager, SlotA, KeyOnlyInA);
        SeedSlot(manager, SlotB, KeyOnlyInB);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void SwitchSelectedSlot_LoadsNewSlotData_NoStaleCarryOver()
    {
        var ops = NewOps();

        ops.SelectSlot(SlotA);
        Assert.That(ops.SlotData.Keys, Has.Member(KeyOnlyInA));

        ops.SelectSlot(SlotB);

        Assert.That(ops.ActiveSlot, Is.EqualTo(SlotB));
        Assert.That(ops.SlotData.Keys, Has.Member(KeyOnlyInB));
        Assert.That(ops.SlotData.Keys, Has.No.Member(KeyOnlyInA));
    }

    [Test]
    public void SwitchBackToPreviousSlot_RestoresItsData()
    {
        var ops = NewOps();

        ops.SelectSlot(SlotA);
        ops.SelectSlot(SlotB);
        ops.SelectSlot(SlotA);

        Assert.That(ops.ActiveSlot, Is.EqualTo(SlotA));
        Assert.That(ops.SlotData.Keys, Has.Member(KeyOnlyInA));
        Assert.That(ops.SlotData.Keys, Has.No.Member(KeyOnlyInB));
    }

    [Test]
    public void DeleteSelectedSlot_FallsBackAndClearsStaleState()
    {
        var ops = NewOps();
        ops.SelectSlot(SlotA);
        Assert.That(ops.SlotData.Keys, Has.Member(KeyOnlyInA));

        ops.DeleteActiveSlot();

        Assert.That(ops.ActiveSlot, Is.Not.EqualTo(SlotA));
        Assert.That(ops.ActiveSlot, Is.EqualTo(_config.DefaultSlot));
        Assert.That(ops.AvailableSlots, Has.No.Member(SlotA));
        Assert.That(ops.SlotData.Keys, Has.No.Member(KeyOnlyInA));
        Assert.That(ops.LastError, Is.Empty);
    }

    [Test]
    public void DeleteSelectedSlot_LeavesOtherSlotsSelectable()
    {
        var ops = NewOps();
        ops.SelectSlot(SlotA);

        ops.DeleteActiveSlot();
        ops.SelectSlot(SlotB);

        Assert.That(ops.ActiveSlot, Is.EqualTo(SlotB));
        Assert.That(ops.SlotData.Keys, Has.Member(KeyOnlyInB));
    }

    [Test]
    public void DeleteSelectedSlot_VersionPanelNoLongerShowsDeletedSlot()
    {
        var ops = NewOps();
        var panel = new DSMSlotVersionPanel();
        panel.Refresh(_config, ops.AvailableSlots, ops.ResolveSlotPath, CurrentVersion);
        Assert.That(panel.InspectedSlots, Has.Member(SlotA));

        ops.SelectSlot(SlotA);
        ops.DeleteActiveSlot();
        panel.Refresh(_config, ops.AvailableSlots, ops.ResolveSlotPath, CurrentVersion);

        Assert.That(panel.InspectedSlots, Has.No.Member(SlotA));
        Assert.That(panel.InspectedSlots, Has.Member(SlotB));
    }

    private DSMManagerSlotOps NewOps()
    {
        var ops = new DSMManagerSlotOps();
        ops.BindConfig(_config, null);
        ops.ResetDefaults(new List<DSMDataEntry>());
        ops.DiscoverSlots();
        return ops;
    }

    private static void SeedSlot(DSMSlotManager manager, string name, string uniqueKey)
    {
        var slot = manager.GetSlot(name);
        slot.Set(uniqueKey, name);
        slot.Save();
    }
}
