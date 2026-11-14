#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCCommandRegistryTests
    {
        private OCConfig _config = null!;
        private OCCommandRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Create();
            _registry = new OCCommandRegistry(() => _config);
        }

        [TearDown]
        public void TearDown()
        {
            _registry.Clear();
            Object.DestroyImmediate(_config);
        }

        private sealed class Owner
        {
            public int Runs;

            [OCCommand("First", Description = "Does the first thing.")]
            private void First() => Runs++;

            [OCCommand("Second")]
            public UniTask Second(CancellationToken ct) => UniTask.CompletedTask;

            public void NotACommand() { }
        }

        private sealed class BadOwner
        {
            [OCCommand("Takes an int")]
            private void TakesInt(int value) { }

            [OCCommand("Good")]
            private void Good() { }
        }

        private static class StaticOwner
        {
            [OCCommand("Static")]
            private static void Static() { }
        }

        private sealed class Ordered
        {
            [OCCommand("Late", Order = 5)]
            private void Late() { }

            [OCCommand("Declared first")]
            private void DeclaredFirst() { }

            [OCCommand("Declared second")]
            private void DeclaredSecond() { }
        }

        private static List<string> Titles(OCCommandRegistry registry)
        {
            var titles = new List<string>();
            foreach (var command in registry.Commands) titles.Add(command.Title);
            return titles;
        }

        [Test]
        public void Register_FindsAttributedInstanceMethods()
        {
            _registry.Register(new Owner());

            CollectionAssert.AreEqual(new[] { "First", "Second" }, Titles(_registry));
            Assert.AreEqual("Does the first thing.", _registry.Commands[0].Description);
        }

        [Test]
        public void Register_Type_FindsAttributedStaticMethods()
        {
            _registry.Register(typeof(StaticOwner));

            CollectionAssert.AreEqual(new[] { "Static" }, Titles(_registry));
            Assert.AreEqual(nameof(StaticOwner), _registry.Commands[0].OwnerName);
        }

        [Test]
        public void Register_SkipsInvalidSignatureWithError()
        {
            LogAssert.Expect(LogType.Error, new Regex("TakesInt is skipped"));

            _registry.Register(new BadOwner());

            CollectionAssert.AreEqual(new[] { "Good" }, Titles(_registry));
        }

        [Test]
        public void Register_SameOwnerTwice_RegistersOnce()
        {
            var owner = new Owner();
            _registry.Register(owner);
            _registry.Register(owner);

            Assert.AreEqual(2, _registry.Commands.Count);
        }

        [Test]
        public void Register_WarnsAboutUnknownAudienceName()
        {
            LogAssert.Expect(LogType.Warning, new Regex("names Audience 'Ghost'"));

            _registry.Register(new object(), "Command", _ => UniTask.CompletedTask, new OCCommandOptions { Audiences = new[] { "Ghost" } });
        }

        [Test]
        public void Unregister_RemovesCommandsAndRaisesChanged()
        {
            var owner = new Owner();
            _registry.Register(owner);
            var changed = 0;
            _registry.Changed += () => changed++;

            _registry.Unregister(owner);

            Assert.AreEqual(0, _registry.Commands.Count);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void DisplayTitle_AddsOwnerNameOnlyWhenTitlesClash()
        {
            var left = new GameObject("Projector L");
            var right = new GameObject("Projector R");
            try
            {
                var a = _registry.Register(left, "Reconnect", _ => UniTask.CompletedTask);
                var b = _registry.Register(right, "Reconnect", _ => UniTask.CompletedTask);
                var unique = _registry.Register(left, "Test pattern", _ => UniTask.CompletedTask);

                Assert.AreEqual("Reconnect (Projector L)", _registry.DisplayTitle(a));
                Assert.AreEqual("Reconnect (Projector R)", _registry.DisplayTitle(b));
                Assert.AreEqual("Test pattern", _registry.DisplayTitle(unique));
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void Commands_SortByOrderThenRegistrationThenDeclaration()
        {
            _registry.Register(new Ordered());
            _registry.Register(new object(), "Registered later", _ => UniTask.CompletedTask);

            CollectionAssert.AreEqual(new[] { "Declared first", "Declared second", "Registered later", "Late" }, Titles(_registry));
        }

        [Test]
        public void Run_Succeeds()
        {
            var owner = new Owner();
            _registry.Register(owner);
            var first = _registry.Commands[0];

            first.RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, owner.Runs);
            Assert.AreEqual(OCCommandStatus.Succeeded, first.Status);
            Assert.IsNull(first.LastError);
        }

        [Test]
        public void Run_Throwing_FailsWithLastError()
        {
            var command = _registry.Register(new object(), "Boom", _ => throw new InvalidOperationException("disk full"));
            LogAssert.Expect(LogType.Exception, new Regex("disk full"));

            command.RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(OCCommandStatus.Failed, command.Status);
            Assert.AreEqual("disk full", command.LastError);
        }

        [Test]
        public void Run_WhileRunning_IsIgnored()
        {
            var gate = new UniTaskCompletionSource();
            var starts = 0;
            var command = _registry.Register(new object(), "Slow", _ =>
            {
                starts++;
                return gate.Task;
            });

            command.Run();
            command.Run();
            Assert.AreEqual(OCCommandStatus.Running, command.Status);

            gate.TrySetResult();

            Assert.AreEqual(1, starts);
            Assert.AreEqual(OCCommandStatus.Succeeded, command.Status);
        }

        [Test]
        public void Unregister_WhileRunning_CancelsWithoutFailing()
        {
            var owner = new object();
            var command = _registry.Register(owner, "Hold", async ct =>
            {
                var gate = new UniTaskCompletionSource();
                using (ct.Register(() => gate.TrySetCanceled()))
                    await gate.Task;
            });

            command.Run();
            Assert.AreEqual(OCCommandStatus.Running, command.Status);

            _registry.Unregister(owner);

            Assert.AreEqual(OCCommandStatus.NeverRun, command.Status);
        }
    }
}
