#nullable enable

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OperatorConsole.Tests
{
    internal sealed class OCPanelRespawnTests
    {
        private OCConfig? _originalConfig;

        [SetUp]
        public void SetUp() => _originalConfig = OC.Config;

        [TearDown]
        public void TearDown()
        {
            if (_originalConfig != null) OC.Configure(_originalConfig);
        }

        [UnityTest]
        public IEnumerator Configure_AfterPanelsSpawned_CreatesPanelsFromNewConfig()
        {
            Assert.IsTrue(OC.Panels.HasSpawned);
            OC.Configure(ConfigWithPanel("First"));
            Assert.IsTrue(OC.Panels.TryGet("First", out var first));

            OC.Configure(ConfigWithPanel("Second"));
            yield return null;

            Assert.IsFalse(OC.Panels.TryGet("First", out _));
            Assert.IsTrue(OC.Panels.TryGet("Second", out _));
            Assert.IsTrue(first == null);
        }

        [UnityTest]
        public IEnumerator SpawnedPanel_ShowAndHide_TogglesVisibility()
        {
            OC.Configure(ConfigWithPanel("Built"));
            Assert.IsTrue(OC.Panels.TryGet("Built", out var panel));
            yield return null;

            panel.Show();
            Assert.IsTrue(panel.IsVisible);
            panel.Hide();
            Assert.IsFalse(panel.IsVisible);
        }

        [UnityTest]
        public IEnumerator SpawnedPanel_WithStartVisible_IsVisibleAfterFirstFrame()
        {
            OC.Configure(ConfigWithPanel("Shown", startVisible: true));
            Assert.IsTrue(OC.Panels.TryGet("Shown", out var panel));
            yield return null;

            Assert.IsTrue(panel.IsVisible);
        }

        private static OCConfig ConfigWithPanel(string name, bool startVisible = false)
        {
            var config = ScriptableObject.CreateInstance<OCConfig>();
            var dir = Path.Combine(Path.GetTempPath(), "dsm-tests", Guid.NewGuid().ToString("N"));
            config.SetForTests(false, 0f, dir, "save.json");
            config.AddPanelForTests(new OCPanelDefinition(name, startVisible));
            return config;
        }
    }
}
