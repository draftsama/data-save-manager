#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCWidgetTests
    {
        private OCConfig? _config;
        private OCStore? _store;

        [TearDown]
        public void TearDown()
        {
            _store?.Dispose();
            if (_config != null)
            {
                if (Directory.Exists(_config.SaveDirectory))
                    Directory.Delete(_config.SaveDirectory, true);
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void Commit_WritesToTheStorePassedToSetup()
        {
            var entry = new OCEntryDefinition { Key = "intKey", Type = OCDataType.Int, DefaultJson = "5", Exposed = true };
            _config = TestConfigs.Create(entry);
            _store = new OCStore(_config, Path.Combine(_config.SaveDirectory, _config.FileName));
            _store.Load();
            var widget = new TestIntWidget();

            widget.Setup(entry, _store);
            widget.CommitForTest(42);
            widget.StopWatching();

            Assert.IsTrue(_store.HasOverride("intKey"));
            Assert.AreEqual(42, _store.Get<int>("intKey"));
        }
    }
}
