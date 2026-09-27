#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMStoreTests
    {
        private readonly List<DSMConfig> _configs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var config in _configs)
            {
                if (Directory.Exists(config.SaveDirectory))
                    Directory.Delete(config.SaveDirectory, true);
                Object.DestroyImmediate(config);
            }
            _configs.Clear();
        }

        private DSMConfig NewConfig(params DSMEntryDefinition[] entries)
        {
            var config = TestConfigs.Create(entries);
            _configs.Add(config);
            return config;
        }

        private static DSMStore NewStore(DSMConfig config) =>
            new(config, Path.Combine(config.SaveDirectory, config.FileName));

        private static DSMEntryDefinition IntEntry(string key = "intKey", string defaultJson = "5") =>
            new() { Key = key, Type = DSMDataType.Int, DefaultJson = defaultJson };

        private static DSMEntryDefinition FloatEntry(string key = "floatKey", string defaultJson = "1.5") =>
            new() { Key = key, Type = DSMDataType.Float, DefaultJson = defaultJson };

        private static DSMEntryDefinition BoolEntry(string key = "boolKey", string defaultJson = "true") =>
            new() { Key = key, Type = DSMDataType.Bool, DefaultJson = defaultJson };

        private static DSMEntryDefinition StringEntry(string key = "stringKey", string defaultJson = "\"hi\"") =>
            new() { Key = key, Type = DSMDataType.String, DefaultJson = defaultJson };

        private static DSMEntryDefinition Vector2Entry(string key = "vector2Key", string defaultJson = "{\"x\":1.0,\"y\":2.0}") =>
            new() { Key = key, Type = DSMDataType.Vector2, DefaultJson = defaultJson };

        private static DSMEntryDefinition Vector3Entry(string key = "vector3Key", string defaultJson = "{\"x\":1.0,\"y\":2.0,\"z\":3.0}") =>
            new() { Key = key, Type = DSMDataType.Vector3, DefaultJson = defaultJson };

        private static DSMEntryDefinition ColorEntry(string key = "colorKey", string defaultJson = "{\"r\":1.0,\"g\":0.0,\"b\":0.0,\"a\":1.0}") =>
            new() { Key = key, Type = DSMDataType.Color, DefaultJson = defaultJson };

        // 1. Get returns the definition default for every type.
        [Test]
        public void Get_ReturnsDefinitionDefault_ForEveryType()
        {
            var config = NewConfig(IntEntry(), FloatEntry(), BoolEntry(), StringEntry(), Vector2Entry(), Vector3Entry(), ColorEntry());
            var store = NewStore(config);

            Assert.AreEqual(5, store.Get<int>("intKey"));
            Assert.AreEqual(1.5f, store.Get<float>("floatKey"));
            Assert.AreEqual(true, store.Get<bool>("boolKey"));
            Assert.AreEqual("hi", store.Get<string>("stringKey"));
            Assert.AreEqual(new Vector2(1f, 2f), store.Get<Vector2>("vector2Key"));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), store.Get<Vector3>("vector3Key"));
            Assert.AreEqual(new Color(1f, 0f, 0f, 1f), store.Get<Color>("colorKey"));

            store.Dispose();
        }

        // 2. Get(key, fallback) returns fallback only for undefined keys without override.
        [Test]
        public void Get_WithFallback_ReturnsFallbackOnlyForUndefinedKeyWithoutOverride()
        {
            var config = NewConfig(IntEntry());
            var store = NewStore(config);

            // Defined key: default wins over the fallback.
            Assert.AreEqual(5, store.Get("intKey", 999));

            // Undefined key with no override: fallback is used, warning logged once.
            LogAssert.Expect(LogType.Warning, new Regex("has no entry definition"));
            Assert.AreEqual(42, store.Get("undefinedKey", 42));

            // Undefined key that now has an override: override wins over the fallback.
            store.Set("undefinedKey", 7);
            Assert.AreEqual(7, store.Get("undefinedKey", 42));

            store.Dispose();
        }

        // 3. Set + Save + new store Load round-trips every type.
        [Test]
        public void Set_Save_Load_RoundTrips_EveryType()
        {
            var config = NewConfig(IntEntry(), FloatEntry(), BoolEntry(), StringEntry(), Vector2Entry(), Vector3Entry(), ColorEntry());

            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 42);
            store.Set("floatKey", 3.25f);
            store.Set("boolKey", false);
            store.Set("stringKey", "bye");
            store.Set("vector2Key", new Vector2(9f, 8f));
            store.Set("vector3Key", new Vector3(9f, 8f, 7f));
            store.Set("colorKey", new Color(0f, 1f, 0f, 0.5f));
            store.Save();
            store.Dispose();

            var reloaded = NewStore(config);
            reloaded.Load();
            Assert.AreEqual(42, reloaded.Get<int>("intKey"));
            Assert.AreEqual(3.25f, reloaded.Get<float>("floatKey"));
            Assert.AreEqual(false, reloaded.Get<bool>("boolKey"));
            Assert.AreEqual("bye", reloaded.Get<string>("stringKey"));
            Assert.AreEqual(new Vector2(9f, 8f), reloaded.Get<Vector2>("vector2Key"));
            Assert.AreEqual(new Vector3(9f, 8f, 7f), reloaded.Get<Vector3>("vector3Key"));
            Assert.AreEqual(new Color(0f, 1f, 0f, 0.5f), reloaded.Get<Color>("colorKey"));
            reloaded.Dispose();
        }

        // 4. Save file contains only overrides.
        [Test]
        public void Save_WritesOnlyOverrides()
        {
            var config = NewConfig(IntEntry(), FloatEntry());
            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 42);
            store.Save();
            store.Dispose();

            var json = File.ReadAllText(Path.Combine(config.SaveDirectory, config.FileName));
            var obj = JObject.Parse(json);
            Assert.IsTrue(obj.ContainsKey("intKey"));
            Assert.IsFalse(obj.ContainsKey("floatKey"));
        }

        // 5. Changing a definition default after save: overridden key keeps override, untouched key follows new default.
        [Test]
        public void OverriddenKey_KeepsOverride_UntouchedKey_FollowsNewDefault()
        {
            var config = NewConfig(IntEntry(), FloatEntry());

            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 42);
            store.Save();
            store.Dispose();

            config.SetEntry(FloatEntry(defaultJson: "9.5"));

            var reloaded = NewStore(config);
            reloaded.Load();
            Assert.AreEqual(42, reloaded.Get<int>("intKey"));
            Assert.AreEqual(9.5f, reloaded.Get<float>("floatKey"));
            reloaded.Dispose();
        }

        // 6. Undefined key: Set/Get work and persist; warning logged exactly once.
        [Test]
        public void UndefinedKey_SetAndGet_Work_And_WarnExactlyOnce()
        {
            var config = NewConfig();
            var store = NewStore(config);
            store.Load();

            LogAssert.Expect(LogType.Warning, new Regex("has no entry definition"));
            store.Set("adhoc", 7);
            Assert.AreEqual(7, store.Get<int>("adhoc"));
            store.Save();

            var json = File.ReadAllText(Path.Combine(config.SaveDirectory, config.FileName));
            Assert.IsTrue(JObject.Parse(json).ContainsKey("adhoc"));

            LogAssert.NoUnexpectedReceived();
            store.Dispose();
        }

        // 7. Corrupt file: Load logs a warning, values are defaults, file content unchanged.
        [Test]
        public void CorruptFile_Load_LogsWarning_DefaultsUsed_FileUnchanged()
        {
            var config = NewConfig(IntEntry());
            var path = Path.Combine(config.SaveDirectory, config.FileName);
            Directory.CreateDirectory(config.SaveDirectory);
            const string corrupt = "{not valid json";
            File.WriteAllText(path, corrupt);

            var store = NewStore(config);
            LogAssert.Expect(LogType.Warning, new Regex("failed to parse save file"));
            store.Load();

            Assert.AreEqual(5, store.Get<int>("intKey"));
            Assert.AreEqual(corrupt, File.ReadAllText(path));
            store.Dispose();
        }

        // 8. Reset(key) / ResetAll() remove overrides and values fall back to defaults.
        [Test]
        public void Reset_And_ResetAll_RemoveOverrides_FallBackToDefaults()
        {
            var config = NewConfig(IntEntry(), FloatEntry());
            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 100);
            store.Set("floatKey", 2.5f);

            store.Reset("intKey");
            Assert.AreEqual(5, store.Get<int>("intKey"));
            Assert.AreEqual(2.5f, store.Get<float>("floatKey"));

            store.Set("intKey", 100);
            store.ResetAll();
            Assert.AreEqual(5, store.Get<int>("intKey"));
            Assert.AreEqual(1.5f, store.Get<float>("floatKey"));

            store.Dispose();
        }

        // 9. Set with an equal value does not set IsDirty (after a Save cleared it).
        [Test]
        public void Set_WithEqualValue_DoesNotMarkDirty()
        {
            var config = NewConfig(IntEntry());
            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 42);
            store.Save();
            Assert.IsFalse(store.IsDirty);

            store.Set("intKey", 42);
            Assert.IsFalse(store.IsDirty);

            store.Dispose();
        }

        // 10. Watch-equivalent notification: fires once per Set, none for an equal Set, once on Reset.
        // Verified via the internal ChangedForTests hook rather than a live WatchAsync consumer,
        // per the plan's explicit fallback ("pick whichever is deterministic without a PlayerLoop") —
        // driving a real IUniTaskAsyncEnumerable consumer synchronously in an EditMode test could not
        // be verified with confidence without running Unity.
        [Test]
        public void ChangedForTests_FiresOncePerEffectiveChange_NotForEqualSet()
        {
            var config = NewConfig(IntEntry());
            var store = NewStore(config);
            store.Load();

            var values = new List<int>();
            store.ChangedForTests += (_, token) => values.Add(token?.ToObject<int>() ?? 0);

            store.Set("intKey", 42);
            store.Set("intKey", 42); // equal — no notification
            store.Set("intKey", 7);
            store.Reset("intKey"); // back to default (5)

            Assert.AreEqual(new List<int> { 42, 7, 5 }, values);

            store.Dispose();
        }
    }
}
