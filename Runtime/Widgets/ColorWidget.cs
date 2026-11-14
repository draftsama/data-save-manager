#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class ColorWidget : OCWidget<Color>
    {
        private const float SwatchInset = 6f;

        private readonly VisualElement _swatch = new();
        private readonly TextField _hex = new() { isDelayed = true };

        public ColorWidget()
        {
            _swatch.AddToClassList("ocp-swatch");
            _hex.textEdition.placeholder = "#RRGGBBAA";
            _hex.RegisterValueChangedCallback(e => Apply(e.newValue));
            Field.Add(_swatch);
            Field.Add(Grow(_hex));
        }

        public override void ApplyLayout(OCPanelLayout layout)
        {
            base.ApplyLayout(layout);
            _swatch.style.width = layout.RowHeight - SwatchInset;
            _swatch.style.height = layout.RowHeight - SwatchInset;
        }

        protected override void Show(Color value)
        {
            var hex = "#" + (value.a >= 1f
                ? ColorUtility.ToHtmlStringRGB(value)
                : ColorUtility.ToHtmlStringRGBA(value));
            _hex.SetValueWithoutNotify(hex);
            _swatch.style.backgroundColor = value;
        }

        private void Apply(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length > 0 && trimmed[0] != '#') trimmed = "#" + trimmed;

            if (ColorUtility.TryParseHtmlString(trimmed, out var color)) Commit(color);
            else Show(Store.Get(Key, Color.white));
        }
    }
}
