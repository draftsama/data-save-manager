#nullable enable

using System;
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
    }
}
