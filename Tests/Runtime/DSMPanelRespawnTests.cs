#nullable enable

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DataSaveManager.Tests
{
    internal sealed class DSMPanelRespawnTests
    {
        private GameObject? _templateParent;
        private DSMConfig? _originalConfig;

        [SetUp]
        public void SetUp()
        {
            _originalConfig = DSM.Config;
            // An inactive parent keeps the template's Awake from running, while its instances start active.
            _templateParent = new GameObject("Panel Template Parent");
            _templateParent.SetActive(false);
            var template = new GameObject("Panel Template");
            template.transform.SetParent(_templateParent.transform, false);
            template.AddComponent<DSMRuntimePanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_originalConfig != null) DSM.Configure(_originalConfig);
            if (_templateParent != null) Object.Destroy(_templateParent);
        }

        [UnityTest]
        public IEnumerator Configure_AfterPanelsSpawned_CreatesPanelsFromNewConfig()
        {
            Assert.IsTrue(DSM.Panels.HasSpawned);
            DSM.Configure(ConfigWithPanel("First"));
            Assert.IsTrue(DSM.Panels.TryGet("First", out var first));

            DSM.Configure(ConfigWithPanel("Second"));
            yield return null;

            Assert.IsFalse(DSM.Panels.TryGet("First", out _));
            Assert.IsTrue(DSM.Panels.TryGet("Second", out _));
            Assert.IsTrue(first == null);
        }

        private DSMConfig ConfigWithPanel(string name)
        {
            var config = ScriptableObject.CreateInstance<DSMConfig>();
            var dir = Path.Combine(Path.GetTempPath(), "dsm-tests", Guid.NewGuid().ToString("N"));
            config.SetForTests(false, 0f, dir, "save.json");
            var template = _templateParent!.GetComponentInChildren<DSMRuntimePanel>(true);
            config.AddPanelForTests(new DSMPanelDefinition(name, template));
            return config;
        }
    }
}
