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
        public void Shows_Command_MatchesAudienceNamesThroughTheConfig()
        {
            var config = TestConfigs.Create();
            config.AddAudienceForTests(new DSMAudience("tech-id", "Technician"));
            config.AddAudienceForTests(new DSMAudience("admin-id", "Admin"));
            var registry = new DSMCommandRegistry(() => null);
            try
            {
                var untagged = registry.Register(new object(), "Untagged", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask);
                var tech = registry.Register(new object(), "Tech", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new DSMCommandOptions { Audiences = new[] { "Technician" } });
                var ghost = registry.Register(new object(), "Ghost", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new DSMCommandOptions { Audiences = new[] { "Ghost" } });

                Assert.IsTrue(DSMAudienceFilter.Shows(untagged, new[] { "admin-id" }, config));
                Assert.IsTrue(DSMAudienceFilter.Shows(tech, new[] { "tech-id" }, config));
                Assert.IsFalse(DSMAudienceFilter.Shows(tech, new[] { "admin-id" }, config));
                Assert.IsTrue(DSMAudienceFilter.Shows(ghost, new string[0], config));
                Assert.IsFalse(DSMAudienceFilter.Shows(ghost, new[] { "admin-id" }, config));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Shows_FalseWhenEntryNamesOnlyUnknownAudiences()
        {
            Assert.IsFalse(DSMAudienceFilter.Shows(Entry(true, "deleted"), new[] { "a" }));
        }
    }
}
