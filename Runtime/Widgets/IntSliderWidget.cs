#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class IntSliderWidget : OCWidget<int>
    {
        private readonly SliderInt _slider = new();
        private readonly IntegerField _input = new() { isDelayed = true };

        public IntSliderWidget()
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
            _slider.lowValue = Mathf.CeilToInt(entry.RangeMin);
            _slider.highValue = Mathf.FloorToInt(entry.RangeMax);
            _slider.RegisterValueChangedCallback(OnSliderChanged);
        }

        // The slider clamps its position to the Range; the input keeps the real Value.
        protected override void Show(int value)
        {
            _slider.SetValueWithoutNotify(value);
            _input.SetValueWithoutNotify(value);
        }

        private void OnSliderChanged(ChangeEvent<int> e) => Commit(e.newValue);
    }
}
