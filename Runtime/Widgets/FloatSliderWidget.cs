#nullable enable

using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class FloatSliderWidget : OCWidget<float>
    {
        private readonly Slider _slider = new();
        private readonly FloatField _input = new() { isDelayed = true, formatString = "F2" };
        private int _decimals = 2;

        public FloatSliderWidget()
        {
            _input.AddToClassList("ocp-slider-value");
            _input.RegisterValueChangedCallback(e => Commit(e.newValue));
            Field.Add(Grow(_slider));
            Field.Add(_input);
        }

        // Listened to only after the range is set: the range setters re-clamp the value with callbacks on.
        protected override void OnSetup(OCEntryDefinition entry)
        {
            _slider.UnregisterValueChangedCallback(OnSliderChanged);
            _slider.lowValue = entry.RangeMin;
            _slider.highValue = entry.RangeMax;
            _slider.RegisterValueChangedCallback(OnSliderChanged);
        }

        public override void ApplyLayout(OCPanelLayout layout)
        {
            base.ApplyLayout(layout);
            _decimals = layout.FloatDecimals;
            _input.formatString = "F" + _decimals.ToString(CultureInfo.InvariantCulture);
            _input.SetValueWithoutNotify(_input.value);
        }

        // The slider clamps its position to the Range; the input keeps the real Value.
        protected override void Show(float value)
        {
            _slider.SetValueWithoutNotify(value);
            _input.SetValueWithoutNotify(value);
        }

        private void OnSliderChanged(ChangeEvent<float> e) =>
            Commit((float)Math.Round(e.newValue, _decimals, MidpointRounding.AwayFromZero));
    }
}
