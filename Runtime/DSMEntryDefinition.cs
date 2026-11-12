#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>The authored description of an Entry: key, data type, default (as JSON), label, exposed flag.</summary>
    [Serializable]
    public sealed class DSMEntryDefinition
    {
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private DSMDataType _type = DSMDataType.String;
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private bool _exposed;
        [SerializeField] private string _defaultJson = string.Empty;
        [SerializeField] private bool _hasRange;
        [SerializeField] private float _rangeMin;
        [SerializeField] private float _rangeMax;
        [SerializeField] private bool _hasHeader;
        [SerializeField] private string _header = string.Empty;
        [SerializeField] private bool _hasOptions;
        [SerializeField] private List<DSMOption> _options = new();

        public bool HasOptions
        {
            get => _hasOptions;
            set => _hasOptions = value;
        }

        public List<DSMOption> Options
        {
            get => _options;
            set => _options = value;
        }

        [Obsolete("Presentation Hint rules live in DSMPresentation; removed in 3.0.")]
        public bool HasValidOptions => DSMPresentation.IsOptionsValid(DSMHintSettings.From(this));

        [Obsolete("Presentation Hint rules live in DSMPresentation; removed in 3.0.")]
        public int IndexOfOptionValue(int value) => DSMPresentation.IndexOfOption(_options, value);

        public string Key
        {
            get => _key;
            set => _key = value;
        }

        public DSMDataType Type
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

        [Obsolete("Presentation Hint rules live in DSMPresentation; removed in 3.0.")]
        public bool ShowsHeader => DSMPresentation.Present(DSMHintSettings.From(this)).Header != null;

        [Obsolete("Presentation Hint rules live in DSMPresentation; removed in 3.0.")]
        public bool HasValidRange => DSMPresentation.IsRangeValid(DSMHintSettings.From(this));

        public string DisplayLabel => string.IsNullOrEmpty(_label) ? _key : _label;

        public static Type ClrTypeOf(DSMDataType t) => t switch
        {
            DSMDataType.Int => typeof(int),
            DSMDataType.Float => typeof(float),
            DSMDataType.Bool => typeof(bool),
            DSMDataType.String => typeof(string),
            DSMDataType.Vector2 => typeof(Vector2),
            DSMDataType.Vector3 => typeof(Vector3),
            DSMDataType.Color => typeof(Color),
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };

        public static bool TryGetDataType(Type clr, out DSMDataType t)
        {
            if (clr == typeof(int)) { t = DSMDataType.Int; return true; }
            if (clr == typeof(float)) { t = DSMDataType.Float; return true; }
            if (clr == typeof(bool)) { t = DSMDataType.Bool; return true; }
            if (clr == typeof(string)) { t = DSMDataType.String; return true; }
            if (clr == typeof(Vector2)) { t = DSMDataType.Vector2; return true; }
            if (clr == typeof(Vector3)) { t = DSMDataType.Vector3; return true; }
            if (clr == typeof(Color)) { t = DSMDataType.Color; return true; }
            t = default;
            return false;
        }

        /// <summary>The zero-value JSON for <paramref name="type"/>, matching what the type's converter writes.</summary>
        public static string DefaultJsonFor(DSMDataType type) => type switch
        {
            DSMDataType.Int => "0",
            DSMDataType.Float => "0.0",
            DSMDataType.Bool => "false",
            DSMDataType.String => "\"\"",
            DSMDataType.Vector2 => "{\"x\":0.0,\"y\":0.0}",
            DSMDataType.Vector3 => "{\"x\":0.0,\"y\":0.0,\"z\":0.0}",
            DSMDataType.Color => "{\"r\":1.0,\"g\":1.0,\"b\":1.0,\"a\":1.0}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
