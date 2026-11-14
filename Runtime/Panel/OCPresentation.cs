#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OperatorConsole
{
    internal enum OCWidgetKind
    {
        Plain,
        Slider,
        Dropdown
    }

    [Flags]
    internal enum OCOptionIssue
    {
        None = 0,
        BlankLabel = 1,
        DuplicateLabel = 2,
        DuplicateValue = 4
    }

    /// <summary>The Presentation Hint settings of one Entry, as stored in its Entry Definition.</summary>
    internal struct OCHintSettings
    {
        public OCDataType Type;
        public string DefaultJson;
        public bool HasRange;
        public float RangeMin;
        public float RangeMax;
        public bool HasOptions;
        public IReadOnlyList<OCOption> Options;
        public bool HasHeader;
        public string Header;

        public static OCHintSettings From(OCEntryDefinition entry) => new()
        {
            Type = entry.Type,
            DefaultJson = entry.DefaultJson,
            HasRange = entry.HasRange,
            RangeMin = entry.RangeMin,
            RangeMax = entry.RangeMax,
            HasOptions = entry.HasOptions,
            Options = entry.Options,
            HasHeader = entry.HasHeader,
            Header = entry.Header
        };
    }

    /// <summary>How the Runtime Panel shows an Exposed Entry.</summary>
    internal readonly struct OCEntryPresentation
    {
        public readonly OCWidgetKind Widget;
        /// <summary>The Header shown above the Entry's row, or null for none.</summary>
        public readonly string? Header;
        /// <summary>Why the Range is ignored, or null when the Range is valid, off, or not allowed for the Data Type.</summary>
        public readonly string? RangeMessage;
        /// <summary>Why the Options are ignored, or null when they are valid, off, or not allowed for the Data Type.</summary>
        public readonly string? OptionsMessage;

        public OCEntryPresentation(OCWidgetKind widget, string? header, string? rangeMessage, string? optionsMessage)
        {
            Widget = widget;
            Header = header;
            RangeMessage = rangeMessage;
            OptionsMessage = optionsMessage;
        }
    }

    /// <summary>
    /// Decides how Presentation Hints show an Entry on the Runtime Panel, and what an edit to them changes.
    /// Hints never touch the Value; the edit operations return new settings and leave their input alone.
    /// </summary>
    internal static class OCPresentation
    {
        public const string InvalidRangeMessage =
            "Invalid Range: min must be less than max. The Runtime Panel falls back to the plain input.";

        public const string InvalidOptionsMessage =
            "Invalid Options: needs at least one Option, unique values and non-empty labels. The Runtime Panel falls back to the plain input.";

        public static bool AllowsRange(OCDataType type) => type is OCDataType.Int or OCDataType.Float;

        public static bool AllowsOptions(OCDataType type) => type == OCDataType.Int;

        public static bool IsRangeValid(in OCHintSettings s) =>
            s.HasRange && AllowsRange(s.Type) && s.RangeMin < s.RangeMax;

        public static bool IsOptionsValid(in OCHintSettings s)
        {
            if (!s.HasOptions || !AllowsOptions(s.Type) || s.Options.Count == 0) return false;
            foreach (var issue in CheckOptions(s.Options))
                if ((issue & (OCOptionIssue.BlankLabel | OCOptionIssue.DuplicateValue)) != 0) return false;
            return true;
        }

        /// <summary>Options win over a Range; an invalid hint falls back to the next one.</summary>
        public static OCEntryPresentation Present(in OCHintSettings s)
        {
            var optionsValid = IsOptionsValid(s);
            var rangeValid = IsRangeValid(s);

            var widget = optionsValid ? OCWidgetKind.Dropdown
                : rangeValid ? OCWidgetKind.Slider
                : OCWidgetKind.Plain;

            var header = s.HasHeader && !string.IsNullOrWhiteSpace(s.Header) ? s.Header : null;
            var rangeMessage = s.HasRange && AllowsRange(s.Type) && !rangeValid ? InvalidRangeMessage : null;
            var optionsMessage = s.HasOptions && AllowsOptions(s.Type) && !optionsValid ? InvalidOptionsMessage : null;

            return new OCEntryPresentation(widget, header, rangeMessage, optionsMessage);
        }

        /// <summary>One entry per Option, in order. Blank labels and duplicate values invalidate the Options; a duplicate label is only a warning.</summary>
        public static OCOptionIssue[] CheckOptions(IReadOnlyList<OCOption> options)
        {
            var issues = new OCOptionIssue[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                var label = options[i].Label;
                if (string.IsNullOrWhiteSpace(label)) issues[i] |= OCOptionIssue.BlankLabel;

                for (var j = 0; j < options.Count; j++)
                {
                    if (j == i) continue;
                    if (options[j].Value == options[i].Value) issues[i] |= OCOptionIssue.DuplicateValue;
                    if (!string.IsNullOrWhiteSpace(label) && string.Equals(options[j].Label, label, StringComparison.Ordinal))
                        issues[i] |= OCOptionIssue.DuplicateLabel;
                }
            }
            return issues;
        }

        public static string? DescribeLabelIssue(OCOptionIssue issue) =>
            (issue & OCOptionIssue.BlankLabel) != 0 ? "Label cannot be empty."
            : (issue & OCOptionIssue.DuplicateLabel) != 0 ? "Another Option has the same label."
            : null;

        public static string? DescribeValueIssue(OCOptionIssue issue) =>
            (issue & OCOptionIssue.DuplicateValue) != 0 ? "Another Option stores the same value." : null;

        /// <summary>Index of the Option whose int is <paramref name="value"/>, or -1 when none matches.</summary>
        public static int IndexOfOption(IReadOnlyList<OCOption> options, int value)
        {
            for (var i = 0; i < options.Count; i++)
                if (options[i].Value == value) return i;
            return -1;
        }

        /// <summary>Resets the Default to the new type's zero value and turns off hints the new type cannot carry.</summary>
        public static OCHintSettings ChangeType(in OCHintSettings s, OCDataType type)
        {
            var next = s;
            next.Type = type;
            next.DefaultJson = OCEntryDefinition.DefaultJsonFor(type);
            if (!AllowsRange(type)) next.HasRange = false;
            if (!AllowsOptions(type)) next.HasOptions = false;
            return next;
        }

        /// <summary>Turns Options off; an unset (0..0) Range starts at 0-1 for Float or 0-100 for Int, widened to contain the Default.</summary>
        public static OCHintSettings TurnRangeOn(in OCHintSettings s)
        {
            var next = s;
            next.HasRange = true;
            next.HasOptions = false;
            if (s.RangeMin == 0f && s.RangeMax == 0f)
                (next.RangeMin, next.RangeMax) = InitialRange(s.Type, s.DefaultJson);
            return next;
        }

        /// <summary>Turns the Range off; with no Options yet, adds a first Option holding the Default.</summary>
        public static OCHintSettings TurnOptionsOn(in OCHintSettings s)
        {
            var next = s;
            next.HasOptions = true;
            next.HasRange = false;
            if (s.Options.Count == 0)
            {
                var value = int.TryParse(s.DefaultJson, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
                next.Options = new List<OCOption> { NewOption(0, value) };
            }
            return next;
        }

        /// <summary>Appends an Option holding the next free int: one above the largest, or 0 when there are none.</summary>
        public static OCHintSettings AddOption(in OCHintSettings s)
        {
            var options = new List<OCOption>(s.Options.Count + 1);
            options.AddRange(s.Options);
            options.Add(NewOption(options.Count, NextOptionValue(s.Options)));

            var next = s;
            next.Options = options;
            return next;
        }

        private static OCOption NewOption(int index, int value) => new($"Option {index + 1}", value);

        private static int NextOptionValue(IReadOnlyList<OCOption> options)
        {
            if (options.Count == 0) return 0;
            var max = int.MinValue;
            foreach (var option in options)
                if (option.Value > max) max = option.Value;
            return max == int.MaxValue ? max : max + 1;
        }

        private static (float min, float max) InitialRange(OCDataType type, string defaultJson)
        {
            var isInt = type == OCDataType.Int;
            var min = 0f;
            var max = isInt ? 100f : 1f;

            if (!float.TryParse(defaultJson, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
                return (min, max);

            if (value < min) min = isInt ? (float)Math.Floor(value) : value;
            if (value > max) max = isInt ? (float)Math.Ceiling(value) : value;
            return (min, max);
        }
    }
}
