#nullable enable

using NUnit.Framework;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMAudienceFilterTests
    {
        private static DSMEntryDefinition Entry(bool exposed, params string[] audienceIds)
        {
            var entry = new DSMEntryDefinition { Key = "k", Exposed = exposed };
            entry.AudienceIds.AddRange(audienceIds);
            return entry;
        }

        [Test]
        public void Shows_FalseWhenNotExposed()
        {
            Assert.IsFalse(DSMAudienceFilter.Shows(Entry(false), new string[0]));
            Assert.IsFalse(DSMAudienceFilter.Shows(Entry(false, "a"), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenPanelServesNoAudience()
        {
            Assert.IsTrue(DSMAudienceFilter.Shows(Entry(true, "a"), new string[0]));
        }

        [Test]
        public void Shows_TrueWhenEntryNamesNoAudience()
        {
            Assert.IsTrue(DSMAudienceFilter.Shows(Entry(true), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenAnyAudienceIsShared()
        {
            Assert.IsTrue(DSMAudienceFilter.Shows(Entry(true, "a", "b"), new[] { "c", "b" }));
        }

        [Test]
        public void Shows_FalseWhenNoAudienceIsShared()
        {
            Assert.IsFalse(DSMAudienceFilter.Shows(Entry(true, "a"), new[] { "b" }));
        }

        [Test]
        public void Shows_FalseWhenEntryNamesOnlyUnknownAudiences()
        {
            Assert.IsFalse(DSMAudienceFilter.Shows(Entry(true, "deleted"), new[] { "a" }));
        }
    }
}
