#nullable enable

using System.IO;
using NUnit.Framework;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMPathsTests
    {
        private const string AppRoot = "/app/root";
        private const string PersistentDataPath = "/persistent/data";

        [Test]
        public void EmptyDirectory_ResolvesUnderPersistentDataPath()
        {
            var result = DSMPaths.Resolve(string.Empty, "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(PersistentDataPath, "DSM", "save.json"), result);
        }

        [Test]
        public void RootedDirectory_IsKeptAsIs()
        {
            var rooted = Path.Combine(Path.GetPathRoot(Path.GetTempPath()) ?? "/", "custom", "dir");
            var result = DSMPaths.Resolve(rooted, "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(rooted, "save.json"), result);
        }

        [Test]
        public void RelativeDirectory_ResolvesUnderAppRoot()
        {
            var result = DSMPaths.Resolve("saves", "save.json", AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(Path.GetFullPath(Path.Combine(AppRoot, "saves")), "save.json"), result);
        }

        [Test]
        public void EmptyFileName_DefaultsToSaveJson()
        {
            var result = DSMPaths.Resolve(string.Empty, string.Empty, AppRoot, PersistentDataPath);
            Assert.AreEqual(Path.Combine(PersistentDataPath, "DSM", "save.json"), result);
        }
    }
}
