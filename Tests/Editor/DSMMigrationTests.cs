#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

[TestFixture]
public class DSMMigrationTests
{
    private DSMSerializer _serializer = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _serializer = new DSMSerializer();
        _tempDir = Path.Combine(Path.GetTempPath(), $"DSM_MigrationTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        DSM.ClearMigrations();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Save_WritesVersionedEnvelope()
    {
        var slot = NewSlot("envelope", DSMMigrationRunner.Empty);
        slot.Set("hp", 100);
        slot.Save();

        var root = JObject.Parse(File.ReadAllText(SlotPath("envelope")));
        Assert.That(root["version"]!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(root["data"], Is.InstanceOf<JObject>());
        Assert.That((int)root["data"]!["hp"]!, Is.EqualTo(100));
        Assert.That(root["hp"], Is.Null);
    }

    [Test]
    public void SaveThenLoad_RoundTripsValues()
    {
        var slot = NewSlot("roundtrip", DSMMigrationRunner.Empty);
        slot.Set("hp", 100);
        slot.Set("name", "hero");
        slot.Save();

        var reloaded = NewSlot("roundtrip", DSMMigrationRunner.Empty);
        reloaded.Load();
        Assert.That(reloaded.Get("hp", 0), Is.EqualTo(100));
        Assert.That(reloaded.Get("name", ""), Is.EqualTo("hero"));
    }

    [Test]
    public void Load_LegacyFlatFile_MigratesToCurrentVersion()
    {
        File.WriteAllText(SlotPath("legacy"), "{\"hp\":100,\"coins\":5}");
        var slot = NewSlot("legacy", FullRunner());
        slot.Load();

        Assert.That(slot.Get("health", 0), Is.EqualTo(100));
        Assert.That(slot.Get("gold", 0), Is.EqualTo(5));
        Assert.That(slot.Has("hp"), Is.False);
    }

    [Test]
    public void Load_LegacyFlatFile_WritesBackEnvelopeImmediately()
    {
        File.WriteAllText(SlotPath("writeback"), "{\"hp\":100,\"coins\":5}");
        var slot = NewSlot("writeback", FullRunner());
        slot.Load();

        var root = JObject.Parse(File.ReadAllText(SlotPath("writeback")));
        Assert.That((int)root["version"]!, Is.EqualTo(3));
        Assert.That((int)root["data"]!["health"]!, Is.EqualTo(100));
        Assert.That((int)root["data"]!["gold"]!, Is.EqualTo(5));
        Assert.That(root["data"]!["hp"], Is.Null);
    }

    [Test]
    public void Load_RenamedKey_RemappedNotDefaulted()
    {
        File.WriteAllText(SlotPath("remap"), "{\"hp\":100,\"coins\":5}");
        var slot = NewSlot("remap", FullRunner());
        slot.Load();

        Assert.That(slot.Has("hp"), Is.False);
        Assert.That(slot.Get("health", -1), Is.EqualTo(100));
    }

    [Test]
    public void Load_V2Envelope_MigratesStepwiseToV3()
    {
        File.WriteAllText(SlotPath("stepwise"), "{\"version\":2,\"data\":{\"health\":100,\"coins\":5}}");
        var slot = NewSlot("stepwise", FullRunner());
        slot.Load();

        Assert.That(slot.Get("gold", 0), Is.EqualTo(5));
        Assert.That(slot.Get("health", 0), Is.EqualTo(100));
    }

    [Test]
    public void Load_CurrentVersion_NotRewritten()
    {
        var path = SlotPath("noop");
        File.WriteAllText(path, "{\"version\":3,\"data\":{\"health\":100,\"gold\":5}}");
        var before = File.ReadAllBytes(path);

        var slot = NewSlot("noop", FullRunner());
        slot.Load();

        var after = File.ReadAllBytes(path);
        Assert.That(after, Is.EqualTo(before));
        Assert.That(slot.Get("health", 0), Is.EqualTo(100));
        Assert.That(slot.Get("gold", 0), Is.EqualTo(5));
    }

    [Test]
    public void Load_FutureVersion_ThrowsAndLeavesFileUntouched()
    {
        var path = SlotPath("future");
        File.WriteAllText(path, "{\"version\":4,\"data\":{\"health\":100,\"gold\":5}}");
        var before = File.ReadAllBytes(path);

        var slot = NewSlot("future", FullRunner());
        Assert.Throws<DSMSaveVersionException>(() => slot.Load());

        var after = File.ReadAllBytes(path);
        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public void Runner_CurrentVersion_DerivedFromChain()
    {
        Assert.That(new DSMMigrationRunner(Array.Empty<IDSMMigration>()).CurrentVersion, Is.EqualTo(1));
        Assert.That(FullRunner().CurrentVersion, Is.EqualTo(3));
    }

    [Test]
    public void Runner_BrokenChain_Throws()
    {
        Assert.Throws<DSMSaveVersionException>(
            () => new DSMMigrationRunner(new IDSMMigration[] { new V2ToV3() }));
    }

    [Test]
    public void DSM_RegisterMigration_DoesNotThrowBeforeFirstUse()
    {
        DSM.ClearMigrations();
        Assert.DoesNotThrow(() => DSM.RegisterMigration(new V1ToV2()));
    }

    [TestCase("slot-legacy-v1")]
    [TestCase("slot-v2")]
    [TestCase("slot-v3")]
    public void Load_CommittedFixture_MigratesToCurrentPayload(string fixture)
    {
        // Copy the committed fixture into the temp dir first — the load-time write-back
        // must never mutate the checked-in fixture file.
        File.Copy(Path.Combine(FixturesDir(), $"{fixture}.json"), SlotPath(fixture));

        var slot = NewSlot(fixture, FullRunner());
        slot.Load();

        Assert.That(slot.Get("health", 0), Is.EqualTo(100));
        Assert.That(slot.Get("gold", 0), Is.EqualTo(5));
    }

    private static DSMMigrationRunner FullRunner() =>
        new DSMMigrationRunner(new IDSMMigration[] { new V1ToV2(), new V2ToV3() });

    private DSMSlot NewSlot(string name, DSMMigrationRunner runner) =>
        new DSMSlot(name, DSMTestConfig.Create(), _serializer, _tempDir, null, runner);

    private string SlotPath(string name) => Path.Combine(_tempDir, $"{name}.json");

    private static string FixturesDir() =>
        Path.Combine(Application.dataPath, "DataSaveManager", "Tests", "Editor", "TestFixtures");

    private sealed class V1ToV2 : IDSMMigration
    {
        public int FromVersion => 1;

        public void Migrate(JObject data)
        {
            data["health"] = data["hp"];
            data.Remove("hp");
        }
    }

    private sealed class V2ToV3 : IDSMMigration
    {
        public int FromVersion => 2;

        public void Migrate(JObject data)
        {
            data["gold"] = data["coins"];
            data.Remove("coins");
        }
    }
}
