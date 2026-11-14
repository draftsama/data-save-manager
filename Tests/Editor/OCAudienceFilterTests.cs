#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCAudienceFilterTests
    {
        private static OCEntryDefinition Entry(bool exposed, params string[] audienceIds)
        {
            var entry = new OCEntryDefinition { Key = "k", Exposed = exposed };
            entry.AudienceIds.AddRange(audienceIds);
            return entry;
        }

        [Test]
        public void Shows_FalseWhenNotExposed()
        {
            Assert.IsFalse(OCAudienceFilter.Shows(Entry(false), new string[0]));
            Assert.IsFalse(OCAudienceFilter.Shows(Entry(false, "a"), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenPanelServesNoAudience()
        {
            Assert.IsTrue(OCAudienceFilter.Shows(Entry(true, "a"), new string[0]));
        }

        [Test]
        public void Shows_TrueWhenEntryNamesNoAudience()
        {
            Assert.IsTrue(OCAudienceFilter.Shows(Entry(true), new[] { "a" }));
        }

        [Test]
        public void Shows_TrueWhenAnyAudienceIsShared()
        {
            Assert.IsTrue(OCAudienceFilter.Shows(Entry(true, "a", "b"), new[] { "c", "b" }));
        }

        [Test]
        public void Shows_FalseWhenNoAudienceIsShared()
        {
            Assert.IsFalse(OCAudienceFilter.Shows(Entry(true, "a"), new[] { "b" }));
        }

        [Test]
        public void Shows_Command_MatchesAudienceNamesThroughTheConfig()
        {
            var config = TestConfigs.Create();
            config.AddAudienceForTests(new OCAudience("tech-id", "Technician"));
            config.AddAudienceForTests(new OCAudience("admin-id", "Admin"));
            var registry = new OCCommandRegistry(() => null);
            try
            {
                var untagged = registry.Register(new object(), "Untagged", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask);
                var tech = registry.Register(new object(), "Tech", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new OCCommandOptions { Audiences = new[] { "Technician" } });
                var ghost = registry.Register(new object(), "Ghost", _ => Cysharp.Threading.Tasks.UniTask.CompletedTask, new OCCommandOptions { Audiences = new[] { "Ghost" } });

                Assert.IsTrue(OCAudienceFilter.Shows(untagged, new[] { "admin-id" }, config));
                Assert.IsTrue(OCAudienceFilter.Shows(tech, new[] { "tech-id" }, config));
                Assert.IsFalse(OCAudienceFilter.Shows(tech, new[] { "admin-id" }, config));
                Assert.IsTrue(OCAudienceFilter.Shows(ghost, new string[0], config));
                Assert.IsFalse(OCAudienceFilter.Shows(ghost, new[] { "admin-id" }, config));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Shows_FalseWhenEntryNamesOnlyUnknownAudiences()
        {
            Assert.IsFalse(OCAudienceFilter.Shows(Entry(true, "deleted"), new[] { "a" }));
        }
    }
}
