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

        // ── InitialRange ─────────────────────────────────────────────────────

        [Test]
        public void InitialRange_Float_DefaultInsideUnitRange_UsesZeroToOne()
        {
            var (min, max) = DSMEntryRules.InitialRange(DSMDataType.Float, "0.5");

            Assert.AreEqual(0f, min);
            Assert.AreEqual(1f, max);
        }

        [Test]
        public void InitialRange_Float_DefaultAboveMax_ExtendsMax()
        {
            var (min, max) = DSMEntryRules.InitialRange(DSMDataType.Float, "5.0");

            Assert.AreEqual(0f, min);
            Assert.GreaterOrEqual(max, 5f);
        }

        [Test]
        public void InitialRange_Int_DefaultInsideRange_UsesZeroToHundred()
        {
            var (min, max) = DSMEntryRules.InitialRange(DSMDataType.Int, "42");

            Assert.AreEqual(0f, min);
            Assert.AreEqual(100f, max);
        }

        [Test]
        public void InitialRange_Int_DefaultBelowMin_ExtendsMin()
        {
            var (min, max) = DSMEntryRules.InitialRange(DSMDataType.Int, "-10");

            Assert.LessOrEqual(min, -10f);
            Assert.AreEqual(100f, max);
        }

        [Test]
        public void InitialRange_UnparsableJson_UsesPlainDefaults()
        {
            Assert.AreEqual((0f, 1f), DSMEntryRules.InitialRange(DSMDataType.Float, "not json"));
            Assert.AreEqual((0f, 100f), DSMEntryRules.InitialRange(DSMDataType.Int, string.Empty));
        }

        // ── HasValidRange ────────────────────────────────────────────────────

        [Test]
        public void HasValidRange_TrueForIntAndFloatWithMinBelowMax()
        {
            Assert.IsTrue(new DSMEntryDefinition { Type = DSMDataType.Int, HasRange = true, RangeMin = 0f, RangeMax = 10f }.HasValidRange);
            Assert.IsTrue(new DSMEntryDefinition { Type = DSMDataType.Float, HasRange = true, RangeMin = -1f, RangeMax = 1f }.HasValidRange);
        }

        [Test]
        public void HasValidRange_FalseWhenMinEqualsMax()
        {
            var entry = new DSMEntryDefinition { Type = DSMDataType.Float, HasRange = true, RangeMin = 3f, RangeMax = 3f };
            Assert.IsFalse(entry.HasValidRange);
        }

        [Test]
        public void HasValidRange_FalseWhenMinAboveMax()
        {
            var entry = new DSMEntryDefinition { Type = DSMDataType.Int, HasRange = true, RangeMin = 10f, RangeMax = 0f };
            Assert.IsFalse(entry.HasValidRange);
        }

        [Test]
        public void HasValidRange_FalseForStringType()
        {
            var entry = new DSMEntryDefinition { Type = DSMDataType.String, HasRange = true, RangeMin = 0f, RangeMax = 10f };
            Assert.IsFalse(entry.HasValidRange);
        }

        [Test]
        public void HasValidRange_FalseWhenRangeNotEnabled()
        {
            var entry = new DSMEntryDefinition { Type = DSMDataType.Float, HasRange = false, RangeMin = 0f, RangeMax = 10f };
            Assert.IsFalse(entry.HasValidRange);
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
