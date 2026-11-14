#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The authored description of an Entry: key, data type, default (as JSON), label, exposed flag.</summary>
    [Serializable]
    public sealed class OCEntryDefinition
    {
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private OCDataType _type = OCDataType.String;
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private bool _exposed;
        [SerializeField] private string _defaultJson = string.Empty;
        [SerializeField] private bool _hasRange;
        [SerializeField] private float _rangeMin;
        [SerializeField] private float _rangeMax;
        [SerializeField] private bool _hasHeader;
        [SerializeField] private string _header = string.Empty;
        [SerializeField] private bool _hasOptions;
        [SerializeField] private List<OCOption> _options = new();
        [SerializeField] private List<string> _audienceIds = new();

        /// <summary>Ids of the Audiences this Entry is meant for; empty means every Audience.</summary>
        public List<string> AudienceIds
        {
            get => _audienceIds;
            set => _audienceIds = value;
        }

        public bool HasOptions
        {
            get => _hasOptions;
            set => _hasOptions = value;
        }

        public List<OCOption> Options
        {
            get => _options;
            set => _options = value;
        }

        [Obsolete("Presentation Hint rules live in OCPresentation; removed in 3.0.")]
        public bool HasValidOptions => OCPresentation.IsOptionsValid(OCHintSettings.From(this));

        [Obsolete("Presentation Hint rules live in OCPresentation; removed in 3.0.")]
        public int IndexOfOptionValue(int value) => OCPresentation.IndexOfOption(_options, value);

        public string Key
        {
            get => _key;
            set => _key = value;
        }

        public OCDataType Type
        {
            get => _type;
            set => _type = value;
        }

        public string Label
        {
            get => _label;
            set => _label = value;
        }

        public bool Exposed
        {
            get => _exposed;
            set => _exposed = value;
        }

        public string DefaultJson
        {
            get => _defaultJson;
            set => _defaultJson = value;
        }

        public bool HasRange
        {
            get => _hasRange;
            set => _hasRange = value;
        }

        public float RangeMin
        {
            get => _rangeMin;
            set => _rangeMin = value;
        }

        public float RangeMax
        {
            get => _rangeMax;
            set => _rangeMax = value;
        }

        public bool HasHeader
        {
            get => _hasHeader;
            set => _hasHeader = value;
        }

        public string Header
        {
            get => _header;
            set => _header = value;
        }

        [Obsolete("Presentation Hint rules live in OCPresentation; removed in 3.0.")]
        public bool ShowsHeader => OCPresentation.Present(OCHintSettings.From(this)).Header != null;

        [Obsolete("Presentation Hint rules live in OCPresentation; removed in 3.0.")]
        public bool HasValidRange => OCPresentation.IsRangeValid(OCHintSettings.From(this));

        public string DisplayLabel => string.IsNullOrEmpty(_label) ? _key : _label;

        public static Type ClrTypeOf(OCDataType t) => t switch
        {
            OCDataType.Int => typeof(int),
            OCDataType.Float => typeof(float),
            OCDataType.Bool => typeof(bool),
            OCDataType.String => typeof(string),
            OCDataType.Vector2 => typeof(Vector2),
            OCDataType.Vector3 => typeof(Vector3),
            OCDataType.Color => typeof(Color),
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };

        public static bool TryGetDataType(Type clr, out OCDataType t)
        {
            if (clr == typeof(int)) { t = OCDataType.Int; return true; }
            if (clr == typeof(float)) { t = OCDataType.Float; return true; }
            if (clr == typeof(bool)) { t = OCDataType.Bool; return true; }
            if (clr == typeof(string)) { t = OCDataType.String; return true; }
            if (clr == typeof(Vector2)) { t = OCDataType.Vector2; return true; }
            if (clr == typeof(Vector3)) { t = OCDataType.Vector3; return true; }
            if (clr == typeof(Color)) { t = OCDataType.Color; return true; }
            t = default;
            return false;
        }

        /// <summary>The zero-value JSON for <paramref name="type"/>, matching what the type's converter writes.</summary>
        public static string DefaultJsonFor(OCDataType type) => type switch
        {
            OCDataType.Int => "0",
            OCDataType.Float => "0.0",
            OCDataType.Bool => "false",
            OCDataType.String => "\"\"",
            OCDataType.Vector2 => "{\"x\":0.0,\"y\":0.0}",
            OCDataType.Vector3 => "{\"x\":0.0,\"y\":0.0,\"z\":0.0}",
            OCDataType.Color => "{\"r\":1.0,\"g\":1.0,\"b\":1.0,\"a\":1.0}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
