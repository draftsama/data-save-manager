#nullable enable

using System.Collections.Generic;
using System.IO;
using OperatorConsole.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCEntryRulesTests
    {
        private readonly List<OCConfig> _configs = new();

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

        private OCConfig NewConfig(params OCEntryDefinition[] entries)
        {
            var config = TestConfigs.Create(entries);
            _configs.Add(config);
            return config;
        }

        private static OCStore NewStore(OCConfig config) =>
            new(config, Path.Combine(config.SaveDirectory, config.FileName));

        private static OCEntryDefinition Entry(string key, OCDataType type) =>
            new() { Key = key, Type = type, DefaultJson = OCEntryDefinition.DefaultJsonFor(type) };

        [Test]
        public void EntriesNamingAudience_ListsOnlyEntriesThatNameIt()
        {
            var named = Entry("named", OCDataType.Int);
            named.AudienceIds.Add("technician");
            var other = Entry("other", OCDataType.Int);
            other.AudienceIds.Add("admin");
            var config = NewConfig(named, other, Entry("untagged", OCDataType.Int));

            CollectionAssert.AreEqual(new[] { "named" }, OCEntryRules.EntriesNamingAudience(config, "technician"));
            CollectionAssert.IsEmpty(OCEntryRules.EntriesNamingAudience(config, "unused"));
        }

        // ── ValidateNewKey ───────────────────────────────────────────────────

        [Test]
        public void ValidateNewKey_RejectsEmptyOrWhitespaceKey()
        {
            var config = NewConfig();

            Assert.IsNotNull(OCEntryRules.ValidateNewKey(config, string.Empty));
            Assert.IsNotNull(OCEntryRules.ValidateNewKey(config, "   "));
        }

        [Test]
        public void ValidateNewKey_RejectsKeyContainingWhitespace()
        {
            var config = NewConfig();
            Assert.IsNotNull(OCEntryRules.ValidateNewKey(config, "foo bar"));
        }

        [Test]
        public void ValidateNewKey_RejectsDuplicateKey()
        {
            var config = NewConfig(Entry("intKey", OCDataType.Int));
            Assert.IsNotNull(OCEntryRules.ValidateNewKey(config, "intKey"));
        }

        [Test]
        public void ValidateNewKey_AcceptsValidNewKey()
        {
            var config = NewConfig(Entry("intKey", OCDataType.Int));
            Assert.IsNull(OCEntryRules.ValidateNewKey(config, "floatKey"));
        }

        // ── DuplicateKeys ────────────────────────────────────────────────────

        [Test]
        public void DuplicateKeys_FindsDuplicateKeys()
        {
            var config = NewConfig(
                Entry("dup", OCDataType.Int),
                Entry("dup", OCDataType.Float),
                Entry("unique", OCDataType.Bool));

            var duplicates = OCEntryRules.DuplicateKeys(config);

            Assert.AreEqual(1, duplicates.Count);
            Assert.IsTrue(duplicates.Contains("dup"));
        }

        [Test]
        public void DuplicateKeys_EmptyWhenNoneDuplicated()
        {
            var config = NewConfig(Entry("a", OCDataType.Int), Entry("b", OCDataType.Int));
            Assert.AreEqual(0, OCEntryRules.DuplicateKeys(config).Count);
        }

        // ── OCWidgets ───────────────────────────────────────────────────────

        [Test]
        public void Create_FallsBackToPlainWhenKindDoesNotApplyToType()
        {
            Assert.IsInstanceOf<DropdownWidget>(OCWidgets.Create(OCWidgetKind.Dropdown, OCDataType.Int));
            Assert.IsInstanceOf<StringWidget>(OCWidgets.Create(OCWidgetKind.Dropdown, OCDataType.String));
            Assert.IsInstanceOf<FloatSliderWidget>(OCWidgets.Create(OCWidgetKind.Slider, OCDataType.Float));
            Assert.IsInstanceOf<BoolWidget>(OCWidgets.Create(OCWidgetKind.Slider, OCDataType.Bool));
        }

        // ── DefaultJsonFor round-trip ────────────────────────────────────────

        [Test]
        public void DefaultJsonFor_RoundTrips_ForEveryType()
        {
            AssertRoundTrip(OCDataType.Int, 0);
            AssertRoundTrip(OCDataType.Float, 0f);
            AssertRoundTrip(OCDataType.Bool, false);
            AssertRoundTrip(OCDataType.String, string.Empty);
            AssertRoundTrip(OCDataType.Vector2, Vector2.zero);
            AssertRoundTrip(OCDataType.Vector3, Vector3.zero);
            AssertRoundTrip(OCDataType.Color, Color.white);
        }

        [Test]
        public void EveryDataType_HasClrTypeAndDefaultJson()
        {
            foreach (OCDataType type in System.Enum.GetValues(typeof(OCDataType)))
            {
                Assert.That(OCEntryDefinition.TryGetDataType(OCEntryDefinition.ClrTypeOf(type), out var mapped), Is.True, type.ToString());
                Assert.That(mapped, Is.EqualTo(type));
                Assert.DoesNotThrow(() => JToken.Parse(OCEntryDefinition.DefaultJsonFor(type)), type.ToString());
            }
        }

        private void AssertRoundTrip<T>(OCDataType type, T expected)
        {
            var config = NewConfig(Entry("key", type));
            var store = NewStore(config);

            Assert.AreEqual(expected, store.Get<T>("key"));

            store.Dispose();
        }

        // ── OCStore.SetToken ────────────────────────────────────────────────

        [Test]
        public void SetToken_EqualToken_IsNoOp()
        {
            var config = NewConfig(Entry("intKey", OCDataType.Int));
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
            var config = NewConfig(Entry("intKey", OCDataType.Int));
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
