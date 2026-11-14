#nullable enable

using System.Collections.Generic;
using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCPresentationTests
    {
        private static OCHintSettings Settings(OCDataType type, string? defaultJson = null) => new()
        {
            Type = type,
            DefaultJson = defaultJson ?? OCEntryDefinition.DefaultJsonFor(type),
            Options = new List<OCOption>(),
            Header = string.Empty
        };

        private static OCHintSettings WithRange(OCDataType type, float min, float max)
        {
            var s = Settings(type);
            s.HasRange = true;
            s.RangeMin = min;
            s.RangeMax = max;
            return s;
        }

        private static OCHintSettings WithOptions(params (string label, int value)[] options)
        {
            var s = Settings(OCDataType.Int);
            s.HasOptions = true;
            var list = new List<OCOption>();
            foreach (var (label, value) in options) list.Add(new OCOption(label, value));
            s.Options = list;
            return s;
        }

        [Test]
        public void AllowedHints_RangeOnIntAndFloat_OptionsOnIntOnly()
        {
            foreach (var type in new[] { OCDataType.Bool, OCDataType.String, OCDataType.Vector2, OCDataType.Vector3, OCDataType.Color })
            {
                Assert.IsFalse(OCPresentation.AllowsRange(type), type.ToString());
                Assert.IsFalse(OCPresentation.AllowsOptions(type), type.ToString());
            }

            Assert.IsTrue(OCPresentation.AllowsRange(OCDataType.Int));
            Assert.IsTrue(OCPresentation.AllowsRange(OCDataType.Float));
            Assert.IsTrue(OCPresentation.AllowsOptions(OCDataType.Int));
            Assert.IsFalse(OCPresentation.AllowsOptions(OCDataType.Float));
        }

        [Test]
        public void Present_ValidRange_ShowsSlider_OnIntAndFloat()
        {
            Assert.AreEqual(OCWidgetKind.Slider, OCPresentation.Present(WithRange(OCDataType.Int, 0f, 10f)).Widget);
            Assert.AreEqual(OCWidgetKind.Slider, OCPresentation.Present(WithRange(OCDataType.Float, -1f, 1f)).Widget);
        }

        [Test]
        public void Present_ValidOptions_WinOverValidRange()
        {
            var s = WithOptions(("Easy", 10), ("Hard", 30));
            s.HasRange = true;
            s.RangeMin = 0f;
            s.RangeMax = 100f;

            Assert.AreEqual(OCWidgetKind.Dropdown, OCPresentation.Present(s).Widget);
        }

        [Test]
        public void Present_InvalidRange_FallsBackToPlain_WithMessage()
        {
            foreach (var (min, max) in new[] { (5f, 5f), (10f, 0f) })
            {
                var p = OCPresentation.Present(WithRange(OCDataType.Float, min, max));

                Assert.AreEqual(OCWidgetKind.Plain, p.Widget);
                Assert.AreEqual(OCPresentation.InvalidRangeMessage, p.RangeMessage);
            }
        }

        [Test]
        public void Present_InvalidOptions_FallBackToRange_WithMessage()
        {
            foreach (var s in new[] { WithOptions(("A", 1), ("B", 1)), WithOptions(("A", 1), ("  ", 2)), WithOptions() })
            {
                var withRange = s;
                withRange.HasRange = true;
                withRange.RangeMin = 0f;
                withRange.RangeMax = 10f;

                var p = OCPresentation.Present(withRange);

                Assert.AreEqual(OCWidgetKind.Slider, p.Widget);
                Assert.AreEqual(OCPresentation.InvalidOptionsMessage, p.OptionsMessage);
                Assert.IsNull(p.RangeMessage);
            }
        }

        [Test]
        public void Present_IgnoresHints_TheTypeCannotCarry_WithoutMessage()
        {
            var range = WithRange(OCDataType.String, 5f, 0f);
            var options = WithOptions(("A", 1));
            options.Type = OCDataType.Float;

            var rangePresentation = OCPresentation.Present(range);
            var optionsPresentation = OCPresentation.Present(options);

            Assert.AreEqual(OCWidgetKind.Plain, rangePresentation.Widget);
            Assert.IsNull(rangePresentation.RangeMessage);
            Assert.AreEqual(OCWidgetKind.Plain, optionsPresentation.Widget);
            Assert.IsNull(optionsPresentation.OptionsMessage);
        }

        [Test]
        public void Present_Header_OnlyWhenOnAndNotBlank()
        {
            var s = Settings(OCDataType.Bool);

            s.HasHeader = false;
            s.Header = "Kinect";
            Assert.IsNull(OCPresentation.Present(s).Header);

            s.HasHeader = true;
            s.Header = "   ";
            Assert.IsNull(OCPresentation.Present(s).Header);

            s.Header = "Kinect";
            Assert.AreEqual("Kinect", OCPresentation.Present(s).Header);
        }

        [Test]
        public void ChangeType_ResetsDefault_AndTurnsOffHintsTheNewTypeCannotCarry()
        {
            var s = WithOptions(("A", 1));
            s.DefaultJson = "1";
            s.HasRange = true;

            var asFloat = OCPresentation.ChangeType(s, OCDataType.Float);
            Assert.AreEqual("0.0", asFloat.DefaultJson);
            Assert.IsTrue(asFloat.HasRange);
            Assert.IsFalse(asFloat.HasOptions);

            var asString = OCPresentation.ChangeType(asFloat, OCDataType.String);
            Assert.AreEqual("\"\"", asString.DefaultJson);
            Assert.IsFalse(asString.HasRange);
        }

        [Test]
        public void TurnRangeOn_TurnsOptionsOff_AndSeedsUnsetRangeAroundDefault()
        {
            var options = WithOptions(("A", 1));
            Assert.IsFalse(OCPresentation.TurnRangeOn(options).HasOptions);

            AssertSeeded(OCDataType.Float, "0.5", 0f, 1f);
            AssertSeeded(OCDataType.Float, "5.0", 0f, 5f);
            AssertSeeded(OCDataType.Int, "42", 0f, 100f);
            AssertSeeded(OCDataType.Int, "-10", -10f, 100f);
            AssertSeeded(OCDataType.Float, "not json", 0f, 1f);
            AssertSeeded(OCDataType.Int, string.Empty, 0f, 100f);

            var kept = OCPresentation.TurnRangeOn(WithRange(OCDataType.Int, 3f, 7f));
            Assert.AreEqual((3f, 7f), (kept.RangeMin, kept.RangeMax));
        }

        private static void AssertSeeded(OCDataType type, string defaultJson, float min, float max)
        {
            var next = OCPresentation.TurnRangeOn(Settings(type, defaultJson));
            Assert.IsTrue(next.HasRange);
            Assert.AreEqual((min, max), (next.RangeMin, next.RangeMax), $"{type} {defaultJson}");
        }

        [Test]
        public void TurnOptionsOn_TurnsRangeOff_AndSeedsFirstOptionFromDefault()
        {
            var s = WithRange(OCDataType.Int, 0f, 10f);
            s.DefaultJson = "7";

            var next = OCPresentation.TurnOptionsOn(s);

            Assert.IsTrue(next.HasOptions);
            Assert.IsFalse(next.HasRange);
            Assert.AreEqual(1, next.Options.Count);
            Assert.AreEqual(7, next.Options[0].Value);
            Assert.AreEqual(0, s.Options.Count);

            var existing = WithOptions(("Easy", 10));
            existing.HasOptions = false;
            Assert.AreSame(existing.Options, OCPresentation.TurnOptionsOn(existing).Options);
        }

        [Test]
        public void AddOption_HoldsNextFreeInt_WithoutChangingInput()
        {
            var empty = WithOptions();
            Assert.AreEqual(0, OCPresentation.AddOption(empty).Options[0].Value);
            Assert.AreEqual(0, empty.Options.Count);

            var some = OCPresentation.AddOption(WithOptions(("A", 10), ("B", 30), ("C", 5)));
            Assert.AreEqual(4, some.Options.Count);
            Assert.AreEqual(31, some.Options[3].Value);

            var full = OCPresentation.AddOption(WithOptions(("A", int.MaxValue)));
            Assert.AreEqual(int.MaxValue, full.Options[1].Value);
        }

        [Test]
        public void CheckOptions_FlagsBlankLabels_DuplicateLabels_AndDuplicateValues()
        {
            var issues = OCPresentation.CheckOptions(new[]
            {
                new OCOption("A", 1),
                new OCOption("A", 2),
                new OCOption(" ", 2),
                new OCOption("", 3),
                new OCOption("", 4)
            });

            Assert.AreEqual(OCOptionIssue.DuplicateLabel, issues[0]);
            Assert.AreEqual(OCOptionIssue.DuplicateLabel | OCOptionIssue.DuplicateValue, issues[1]);
            Assert.AreEqual(OCOptionIssue.BlankLabel | OCOptionIssue.DuplicateValue, issues[2]);
            Assert.AreEqual(OCOptionIssue.BlankLabel, issues[3]);
            Assert.AreEqual(OCOptionIssue.BlankLabel, issues[4]);
        }

        [Test]
        public void IndexOfOption_FindsByStoredIntNotPosition()
        {
            var options = new[] { new OCOption("Hard", 30), new OCOption("Easy", 10) };

            Assert.AreEqual(1, OCPresentation.IndexOfOption(options, 10));
            Assert.AreEqual(-1, OCPresentation.IndexOfOption(options, 99));
        }
    }
}
