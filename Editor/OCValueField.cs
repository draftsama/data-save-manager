#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>A typed field for one <see cref="OCDataType"/> that reads and writes <see cref="JToken"/>s through the runtime serializer, so it matches what the store persists.</summary>
    internal sealed class OCValueField : VisualElement
    {
        private static readonly OCSerializer Serializer = new();

        private readonly OCDataType _type;
        private readonly Action<JToken> _changed;
        private Action<JToken> _setWithoutNotify = _ => { };
        private DropdownField? _dropdown;
        private readonly List<OCOption> _options = new();

        /// <param name="options">Shows a dropdown of these Options instead of a number field; only for Int.</param>
        public OCValueField(OCDataType type, IReadOnlyList<OCOption>? options, Action<JToken> changed)
        {
            _type = type;
            _changed = changed;
            AddToClassList("oc-value-field");

            if (type == OCDataType.Int && options is { Count: > 0 })
            {
                _dropdown = new DropdownField();
                SetOptions(options);
                _dropdown.RegisterValueChangedCallback(e =>
                {
                    var index = _dropdown.choices.IndexOf(e.newValue);
                    if (index >= 0) _changed(ToToken(_options[index].Value));
                });
                Add(_dropdown);
                _setWithoutNotify = token =>
                {
                    var index = OCPresentation.IndexOfOption(_options, Read(token, 0));
                    _dropdown.SetValueWithoutNotify(index >= 0 ? _dropdown.choices[index] : string.Empty);
                };
                return;
            }

            switch (type)
            {
                case OCDataType.Int: Use(new IntegerField { isDelayed = true }, 0); break;
                case OCDataType.Float: Use(new FloatField { isDelayed = true }, 0f); break;
                case OCDataType.Bool: Use(new Toggle(), false); break;
                case OCDataType.String: Use(new TextField { isDelayed = true }, string.Empty); break;
                case OCDataType.Vector2: Use(new Vector2Field(), Vector2.zero); break;
                case OCDataType.Vector3: Use(new Vector3Field(), Vector3.zero); break;
                case OCDataType.Color: Use(new ColorField(), Color.white); break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public bool HasFocus => focusController?.focusedElement is VisualElement focused && (focused == this || Contains(focused));

        /// <summary>Shows <paramref name="token"/> without raising the change callback; a field being edited keeps what the user typed.</summary>
        public void SetToken(JToken? token)
        {
            if (HasFocus) return;
            _setWithoutNotify(token ?? DefaultToken(_type));
        }

        public void SetOptions(IReadOnlyList<OCOption> options)
        {
            if (_dropdown == null) return;
            _options.Clear();
            _options.AddRange(options);
            var labels = new List<string>(options.Count);
            foreach (var option in options) labels.Add($"{option.Label} ({option.Value.ToString(CultureInfo.InvariantCulture)})");
            _dropdown.choices = labels;
        }

        private void Use<T>(BaseField<T> field, T fallback)
        {
            field.RegisterValueChangedCallback(e => _changed(ToToken(e.newValue)));
            _setWithoutNotify = token => field.SetValueWithoutNotify(Read(token, fallback));
            Add(field);
        }

        public static JToken DefaultToken(OCDataType type)
        {
            try { return JToken.Parse(OCEntryDefinition.DefaultJsonFor(type)); }
            catch { return JValue.CreateNull(); }
        }

        public static JToken? ParseOrNull(string json)
        {
            try { return JToken.Parse(json); }
            catch { return null; }
        }

        private static T Read<T>(JToken token, T fallback)
        {
            try
            {
                var result = token.ToObject<T>(Serializer.JsonSerializer);
                return result is null ? fallback : result;
            }
            catch { return fallback; }
        }

        private static JToken ToToken<T>(T value) => JToken.FromObject(value!, Serializer.JsonSerializer);
    }
}
