#nullable enable

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OperatorConsole.Tests
{
    internal sealed class OCPanelRespawnTests
    {
        private GameObject? _templateParent;
        private OCConfig? _originalConfig;

        [SetUp]
        public void SetUp()
        {
            _originalConfig = OC.Config;
            // An inactive parent keeps the template's Awake from running, while its instances start active.
            _templateParent = new GameObject("Panel Template Parent");
            _templateParent.SetActive(false);
            var template = new GameObject("Panel Template");
            template.transform.SetParent(_templateParent.transform, false);
            template.AddComponent<OCRuntimePanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_originalConfig != null) OC.Configure(_originalConfig);
            if (_templateParent != null) Object.Destroy(_templateParent);
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

        private OCConfig ConfigWithPanel(string name)
        {
            var config = ScriptableObject.CreateInstance<OCConfig>();
            var dir = Path.Combine(Path.GetTempPath(), "dsm-tests", Guid.NewGuid().ToString("N"));
            config.SetForTests(false, 0f, dir, "save.json");
            var template = _templateParent!.GetComponentInChildren<OCRuntimePanel>(true);
            config.AddPanelForTests(new OCPanelDefinition(name, template));
            return config;
        }
    }
}
