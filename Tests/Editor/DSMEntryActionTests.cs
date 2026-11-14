#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMEntryActionTests
    {
        private DSMConfig _config = null!;
        private DSMStore _store = null!;
        private DSMCommandRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Create(
                new DSMEntryDefinition { Key = "folder", Type = DSMDataType.String, DefaultJson = "\"C:/old\"", Exposed = true },
                new DSMEntryDefinition { Key = "count", Type = DSMDataType.Int, DefaultJson = "1", Exposed = true },
                new DSMEntryDefinition { Key = "hidden", Type = DSMDataType.String, DefaultJson = "\"\"", Exposed = false });
            _store = new DSMStore(_config, Path.Combine(_config.SaveDirectory, _config.FileName));
            _registry = new DSMCommandRegistry(() => _config);
        }

        [TearDown]
        public void TearDown()
        {
            _registry.Clear();
            Object.DestroyImmediate(_config);
        }

        private sealed class EverySignature
        {
            public string? Seen;

            [DSMEntryAction("folder", "None")]
            private string None() => "none";

            [DSMEntryAction("folder", "Token")]
            private UniTask<string> Token(CancellationToken ct) => UniTask.FromResult("token");

            [DSMEntryAction("folder", "Current")]
            private string Current(string current)
            {
                Seen = current;
                return current + "/sub";
            }

            [DSMEntryAction("folder", "Both")]
            private UniTask<string> Both(string current, CancellationToken ct) => UniTask.FromResult(current + "/both");

            [DSMCommand("A Command")]
            private void Command() { }
        }

        private sealed class BadSignature
        {
            [DSMEntryAction("folder", "Wrong parameter")]
            private string WrongParameter(int value) => "x";

            [DSMEntryAction("folder", "Good")]
            private string Good() => "good";
        }

        private sealed class WrongDataType
        {
            [DSMEntryAction("folder", "Returns int")]
            private int ReturnsInt() => 3;
        }

        private static List<string> Titles(IEnumerable<DSMRegisteredEntryAction> actions)
        {
            var titles = new List<string>();
            foreach (var action in actions) titles.Add(action.Title);
            return titles;
        }

        [Test]
        public void Register_FindsEntryActionsOfEverySignatureAndCommandsTogether()
        {
            _registry.Register(new EverySignature());

            CollectionAssert.AreEqual(new[] { "None", "Token", "Current", "Both" }, Titles(_registry.EntryActions));
            Assert.AreEqual(1, _registry.Commands.Count);
            Assert.AreEqual(DSMDataType.String, _registry.EntryActions[0].DataType);
        }

        [Test]
        public void Register_SkipsInvalidSignatureWithError()
        {
            LogAssert.Expect(LogType.Error, new Regex("WrongParameter is skipped"));

            _registry.Register(new BadSignature());

            CollectionAssert.AreEqual(new[] { "Good" }, Titles(_registry.EntryActions));
        }

        [Test]
        public void Register_ValueTypeNotTheEntrysDataType_IsSkippedWithError()
        {
            LogAssert.Expect(LogType.Error, new Regex("produces Int, but the Entry is String"));

            _registry.Register(new WrongDataType());

            Assert.AreEqual(0, _registry.EntryActions.Count);
        }

        [Test]
        public void Register_UnknownOrHiddenEntry_WarnsAndKeepsTheAction()
        {
            LogAssert.Expect(LogType.Warning, new Regex("names Entry 'ghost'"));
            LogAssert.Expect(LogType.Warning, new Regex("'hidden', which is not an Exposed Entry"));

            _registry.Register<string>(new object(), "ghost", "Browse", (current, _) => UniTask.FromResult(current));
            _registry.Register<string>(new object(), "hidden", "Browse", (current, _) => UniTask.FromResult(current));

            Assert.AreEqual(2, _registry.EntryActions.Count);
        }

        [Test]
        public void Run_SetsTheResultFromTheCurrentValue()
        {
            var owner = new EverySignature();
            _registry.Register(owner);
            var current = _registry.EntryActions[2];

            current.RunAsync(_store).GetAwaiter().GetResult();

            Assert.AreEqual("C:/old", owner.Seen);
            Assert.AreEqual("C:/old/sub", _store.Get<string>("folder"));
            Assert.IsTrue(_store.HasOverride("folder"));
            Assert.AreEqual(DSMCommandStatus.Succeeded, current.Status);
        }

        [Test]
        public void Run_Cancelled_LeavesTheValue()
        {
            var action = _registry.Register<string>(new object(), "folder", "Browse", (_, _) => throw new OperationCanceledException())!;

            action.RunAsync(_store).GetAwaiter().GetResult();

            Assert.IsFalse(_store.HasOverride("folder"));
            Assert.AreEqual(DSMCommandStatus.NeverRun, action.Status);
        }

        [Test]
        public void Run_Throwing_FailsWithLastErrorAndLeavesTheValue()
        {
            var action = _registry.Register<int>(new object(), "count", "Fetch", (_, _) => throw new InvalidOperationException("401 Unauthorized"))!;
            LogAssert.Expect(LogType.Exception, new Regex("401 Unauthorized"));

            action.RunAsync(_store).GetAwaiter().GetResult();

            Assert.AreEqual(DSMCommandStatus.Failed, action.Status);
            Assert.AreEqual("401 Unauthorized", action.LastError);
            Assert.IsFalse(_store.HasOverride("count"));
        }

        [Test]
        public void Run_ValueChangedWhileRunning_LastWriteWins()
        {
            var gate = new UniTaskCompletionSource<int>();
            var action = _registry.Register<int>(new object(), "count", "Fetch", (_, _) => gate.Task)!;

            action.Run(_store);
            _store.Set("count", 7);
            gate.TrySetResult(42);

            Assert.AreEqual(42, _store.Get<int>("count"));
        }

        [Test]
        public void Unregister_WhileRunning_CancelsAndLeavesTheValue()
        {
            var owner = new object();
            var gate = new UniTaskCompletionSource<string>();
            var action = _registry.Register<string>(owner, "folder", "Browse", (_, _) => gate.Task)!;

            action.Run(_store);
            Assert.IsTrue(action.IsRunning);

            _registry.Unregister(owner);
            gate.TrySetResult("C:/new");

            Assert.AreEqual(0, _registry.EntryActions.Count);
            Assert.IsFalse(_store.HasOverride("folder"));
            Assert.AreEqual(DSMCommandStatus.NeverRun, action.Status);
        }

        [Test]
        public void DisplayTitle_AddsOwnerNameOnlyWhenTitlesClashOnTheSameEntry()
        {
            var left = new GameObject("Projector L");
            var right = new GameObject("Projector R");
            try
            {
                var a = _registry.Register<int>(left, "count", "Scan", (v, _) => UniTask.FromResult(v))!;
                var b = _registry.Register<int>(right, "count", "Scan", (v, _) => UniTask.FromResult(v))!;
                var otherEntry = _registry.Register<string>(left, "folder", "Scan", (v, _) => UniTask.FromResult(v))!;

                Assert.AreEqual("Scan (Projector L)", _registry.DisplayTitle(a));
                Assert.AreEqual("Scan (Projector R)", _registry.DisplayTitle(b));
                Assert.AreEqual("Scan", _registry.DisplayTitle(otherEntry));
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void EntryActionsFor_ListsOneEntrysActionsByOrder()
        {
            var owner = new object();
            _registry.Register<string>(owner, "folder", "Late", (v, _) => UniTask.FromResult(v), new DSMEntryActionOptions { Order = 5 });
            _registry.Register<string>(owner, "folder", "Early", (v, _) => UniTask.FromResult(v));
            _registry.Register<int>(owner, "count", "Elsewhere", (v, _) => UniTask.FromResult(v));

            CollectionAssert.AreEqual(new[] { "Early", "Late" }, Titles(_registry.EntryActionsFor("folder")));
        }
    }
}
