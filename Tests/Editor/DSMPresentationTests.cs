#nullable enable

using System.Collections.Generic;
using NUnit.Framework;

namespace DataSaveManager.Tests
{
    [TestFixture]
    internal sealed class DSMPresentationTests
    {
        private static DSMHintSettings Settings(DSMDataType type, string? defaultJson = null) => new()
        {
            Type = type,
            DefaultJson = defaultJson ?? DSMEntryDefinition.DefaultJsonFor(type),
            Options = new List<DSMOption>(),
            Header = string.Empty
        };

        private static DSMHintSettings WithRange(DSMDataType type, float min, float max)
        {
            var s = Settings(type);
            s.HasRange = true;
            s.RangeMin = min;
            s.RangeMax = max;
            return s;
        }

        private static DSMHintSettings WithOptions(params (string label, int value)[] options)
        {
            var s = Settings(DSMDataType.Int);
            s.HasOptions = true;
            var list = new List<DSMOption>();
            foreach (var (label, value) in options) list.Add(new DSMOption(label, value));
            s.Options = list;
            return s;
        }

        [Test]
        public void AllowedHints_RangeOnIntAndFloat_OptionsOnIntOnly()
        {
            foreach (var type in new[] { DSMDataType.Bool, DSMDataType.String, DSMDataType.Vector2, DSMDataType.Vector3, DSMDataType.Color })
            {
                Assert.IsFalse(DSMPresentation.AllowsRange(type), type.ToString());
                Assert.IsFalse(DSMPresentation.AllowsOptions(type), type.ToString());
            }

            Assert.IsTrue(DSMPresentation.AllowsRange(DSMDataType.Int));
            Assert.IsTrue(DSMPresentation.AllowsRange(DSMDataType.Float));
            Assert.IsTrue(DSMPresentation.AllowsOptions(DSMDataType.Int));
            Assert.IsFalse(DSMPresentation.AllowsOptions(DSMDataType.Float));
        }

        [Test]
        public void Present_ValidRange_ShowsSlider_OnIntAndFloat()
        {
            Assert.AreEqual(DSMWidgetKind.Slider, DSMPresentation.Present(WithRange(DSMDataType.Int, 0f, 10f)).Widget);
            Assert.AreEqual(DSMWidgetKind.Slider, DSMPresentation.Present(WithRange(DSMDataType.Float, -1f, 1f)).Widget);
        }

        [Test]
        public void Present_ValidOptions_WinOverValidRange()
        {
            var s = WithOptions(("Easy", 10), ("Hard", 30));
            s.HasRange = true;
            s.RangeMin = 0f;
            s.RangeMax = 100f;

            Assert.AreEqual(DSMWidgetKind.Dropdown, DSMPresentation.Present(s).Widget);
        }

        [Test]
        public void Present_InvalidRange_FallsBackToPlain_WithMessage()
        {
            foreach (var (min, max) in new[] { (5f, 5f), (10f, 0f) })
            {
                var p = DSMPresentation.Present(WithRange(DSMDataType.Float, min, max));

                Assert.AreEqual(DSMWidgetKind.Plain, p.Widget);
                Assert.AreEqual(DSMPresentation.InvalidRangeMessage, p.RangeMessage);
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

                var p = DSMPresentation.Present(withRange);

                Assert.AreEqual(DSMWidgetKind.Slider, p.Widget);
                Assert.AreEqual(DSMPresentation.InvalidOptionsMessage, p.OptionsMessage);
                Assert.IsNull(p.RangeMessage);
            }
        }

        [Test]
        public void Present_IgnoresHints_TheTypeCannotCarry_WithoutMessage()
        {
            var range = WithRange(DSMDataType.String, 5f, 0f);
            var options = WithOptions(("A", 1));
            options.Type = DSMDataType.Float;

            var rangePresentation = DSMPresentation.Present(range);
            var optionsPresentation = DSMPresentation.Present(options);

            Assert.AreEqual(DSMWidgetKind.Plain, rangePresentation.Widget);
            Assert.IsNull(rangePresentation.RangeMessage);
            Assert.AreEqual(DSMWidgetKind.Plain, optionsPresentation.Widget);
            Assert.IsNull(optionsPresentation.OptionsMessage);
        }

        [Test]
        public void Present_Header_OnlyWhenOnAndNotBlank()
        {
            var s = Settings(DSMDataType.Bool);

            s.HasHeader = false;
            s.Header = "Kinect";
            Assert.IsNull(DSMPresentation.Present(s).Header);

            s.HasHeader = true;
            s.Header = "   ";
            Assert.IsNull(DSMPresentation.Present(s).Header);

            s.Header = "Kinect";
            Assert.AreEqual("Kinect", DSMPresentation.Present(s).Header);
        }

        [Test]
        public void ChangeType_ResetsDefault_AndTurnsOffHintsTheNewTypeCannotCarry()
        {
            var s = WithOptions(("A", 1));
            s.DefaultJson = "1";
            s.HasRange = true;

            var asFloat = DSMPresentation.ChangeType(s, DSMDataType.Float);
            Assert.AreEqual("0.0", asFloat.DefaultJson);
            Assert.IsTrue(asFloat.HasRange);
            Assert.IsFalse(asFloat.HasOptions);

            var asString = DSMPresentation.ChangeType(asFloat, DSMDataType.String);
            Assert.AreEqual("\"\"", asString.DefaultJson);
            Assert.IsFalse(asString.HasRange);
        }

        [Test]
        public void TurnRangeOn_TurnsOptionsOff_AndSeedsUnsetRangeAroundDefault()
        {
            var options = WithOptions(("A", 1));
            Assert.IsFalse(DSMPresentation.TurnRangeOn(options).HasOptions);

            AssertSeeded(DSMDataType.Float, "0.5", 0f, 1f);
            AssertSeeded(DSMDataType.Float, "5.0", 0f, 5f);
            AssertSeeded(DSMDataType.Int, "42", 0f, 100f);
            AssertSeeded(DSMDataType.Int, "-10", -10f, 100f);
            AssertSeeded(DSMDataType.Float, "not json", 0f, 1f);
            AssertSeeded(DSMDataType.Int, string.Empty, 0f, 100f);

            var kept = DSMPresentation.TurnRangeOn(WithRange(DSMDataType.Int, 3f, 7f));
            Assert.AreEqual((3f, 7f), (kept.RangeMin, kept.RangeMax));
        }

        private static void AssertSeeded(DSMDataType type, string defaultJson, float min, float max)
        {
            var next = DSMPresentation.TurnRangeOn(Settings(type, defaultJson));
            Assert.IsTrue(next.HasRange);
            Assert.AreEqual((min, max), (next.RangeMin, next.RangeMax), $"{type} {defaultJson}");
        }

        [Test]
        public void TurnOptionsOn_TurnsRangeOff_AndSeedsFirstOptionFromDefault()
        {
            var s = WithRange(DSMDataType.Int, 0f, 10f);
            s.DefaultJson = "7";

            var next = DSMPresentation.TurnOptionsOn(s);

            Assert.IsTrue(next.HasOptions);
            Assert.IsFalse(next.HasRange);
            Assert.AreEqual(1, next.Options.Count);
            Assert.AreEqual(7, next.Options[0].Value);
            Assert.AreEqual(0, s.Options.Count);

            var existing = WithOptions(("Easy", 10));
            existing.HasOptions = false;
            Assert.AreSame(existing.Options, DSMPresentation.TurnOptionsOn(existing).Options);
        }

        [Test]
        public void AddOption_HoldsNextFreeInt_WithoutChangingInput()
        {
            var empty = WithOptions();
            Assert.AreEqual(0, DSMPresentation.AddOption(empty).Options[0].Value);
            Assert.AreEqual(0, empty.Options.Count);

            var some = DSMPresentation.AddOption(WithOptions(("A", 10), ("B", 30), ("C", 5)));
            Assert.AreEqual(4, some.Options.Count);
            Assert.AreEqual(31, some.Options[3].Value);

            var full = DSMPresentation.AddOption(WithOptions(("A", int.MaxValue)));
            Assert.AreEqual(int.MaxValue, full.Options[1].Value);
        }

        [Test]
        public void CheckOptions_FlagsBlankLabels_DuplicateLabels_AndDuplicateValues()
        {
            var issues = DSMPresentation.CheckOptions(new[]
            {
                new DSMOption("A", 1),
                new DSMOption("A", 2),
                new DSMOption(" ", 2),
                new DSMOption("", 3),
                new DSMOption("", 4)
            });

            Assert.AreEqual(DSMOptionIssue.DuplicateLabel, issues[0]);
            Assert.AreEqual(DSMOptionIssue.DuplicateLabel | DSMOptionIssue.DuplicateValue, issues[1]);
            Assert.AreEqual(DSMOptionIssue.BlankLabel | DSMOptionIssue.DuplicateValue, issues[2]);
            Assert.AreEqual(DSMOptionIssue.BlankLabel, issues[3]);
            Assert.AreEqual(DSMOptionIssue.BlankLabel, issues[4]);
        }

        [Test]
        public void IndexOfOption_FindsByStoredIntNotPosition()
        {
            var options = new[] { new DSMOption("Hard", 30), new DSMOption("Easy", 10) };

            Assert.AreEqual(1, DSMPresentation.IndexOfOption(options, 10));
            Assert.AreEqual(-1, DSMPresentation.IndexOfOption(options, 99));
        }
    }
}
