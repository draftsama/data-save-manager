#nullable enable

using System.Collections.Generic;
using System.IO;
using DataSaveManager.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMEntryRulesTests
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

        private static DSMEntryDefinition Entry(string key, DSMDataType type) =>
            new() { Key = key, Type = type, DefaultJson = DSMEntryDefinition.DefaultJsonFor(type) };

        // ── ValidateNewKey ───────────────────────────────────────────────────

        [Test]
        public void ValidateNewKey_RejectsEmptyOrWhitespaceKey()
        {
            var config = NewConfig();

            Assert.IsNotNull(DSMEntryRules.ValidateNewKey(config, string.Empty));
            Assert.IsNotNull(DSMEntryRules.ValidateNewKey(config, "   "));
        }

        [Test]
        public void ValidateNewKey_RejectsKeyContainingWhitespace()
        {
            var config = NewConfig();
            Assert.IsNotNull(DSMEntryRules.ValidateNewKey(config, "foo bar"));
        }

        [Test]
        public void ValidateNewKey_RejectsDuplicateKey()
        {
            var config = NewConfig(Entry("intKey", DSMDataType.Int));
            Assert.IsNotNull(DSMEntryRules.ValidateNewKey(config, "intKey"));
        }

        [Test]
        public void ValidateNewKey_AcceptsValidNewKey()
        {
            var config = NewConfig(Entry("intKey", DSMDataType.Int));
            Assert.IsNull(DSMEntryRules.ValidateNewKey(config, "floatKey"));
        }

        // ── DuplicateKeys ────────────────────────────────────────────────────

        [Test]
        public void DuplicateKeys_FindsDuplicateKeys()
        {
            var config = NewConfig(
                Entry("dup", DSMDataType.Int),
                Entry("dup", DSMDataType.Float),
                Entry("unique", DSMDataType.Bool));

            var duplicates = DSMEntryRules.DuplicateKeys(config);

            Assert.AreEqual(1, duplicates.Count);
            Assert.IsTrue(duplicates.Contains("dup"));
        }

        [Test]
        public void DuplicateKeys_EmptyWhenNoneDuplicated()
        {
            var config = NewConfig(Entry("a", DSMDataType.Int), Entry("b", DSMDataType.Int));
            Assert.AreEqual(0, DSMEntryRules.DuplicateKeys(config).Count);
        }

        // ── DefaultJsonFor round-trip ────────────────────────────────────────

        [Test]
        public void DefaultJsonFor_RoundTrips_ForEveryType()
        {
            AssertRoundTrip(DSMDataType.Int, 0);
            AssertRoundTrip(DSMDataType.Float, 0f);
            AssertRoundTrip(DSMDataType.Bool, false);
            AssertRoundTrip(DSMDataType.String, string.Empty);
            AssertRoundTrip(DSMDataType.Vector2, Vector2.zero);
            AssertRoundTrip(DSMDataType.Vector3, Vector3.zero);
            AssertRoundTrip(DSMDataType.Color, Color.white);
        }

        private void AssertRoundTrip<T>(DSMDataType type, T expected)
        {
            var config = NewConfig(Entry("key", type));
            var store = NewStore(config);

            Assert.AreEqual(expected, store.Get<T>("key"));

            store.Dispose();
        }

        // ── DSMStore.SetToken ────────────────────────────────────────────────

        [Test]
        public void SetToken_EqualToken_IsNoOp()
        {
            var config = NewConfig(Entry("intKey", DSMDataType.Int));
            var store = NewStore(config);
            store.Load();
            store.Set("intKey", 5);
            store.Save();
            Assert.IsFalse(store.IsDirty);

            store.SetToken("intKey", JToken.FromObject(5));

            Assert.IsFalse(store.IsDirty);
            Assert.AreEqual(5, store.Get<int>("intKey"));

            store.Dispose();
        }

        [Test]
        public void SetToken_DifferentToken_SetsOverride()
        {
            var config = NewConfig(Entry("intKey", DSMDataType.Int));
            var store = NewStore(config);
            store.Load();

            store.SetToken("intKey", JToken.FromObject(42));

            Assert.IsTrue(store.IsDirty);
            Assert.IsTrue(store.HasOverride("intKey"));
            Assert.AreEqual(42, store.Get<int>("intKey"));

            store.Dispose();
        }
    }
}
