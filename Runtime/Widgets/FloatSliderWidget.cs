#nullable enable

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class FloatSliderWidget : DSMWidget<float>
    {
        [SerializeField] private Slider? _slider;
        [SerializeField] private TMP_InputField? _input;

        private int _decimals = 2;
        private string _format = "F2";
        private float _current;
        private bool _hasValue;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(OnEndEdit);
        }

        protected override void OnSetup(DSMEntryDefinition entry)
        {
            if (_slider == null) return;
            _slider.wholeNumbers = false;
            _slider.minValue = entry.RangeMin;
            _slider.maxValue = entry.RangeMax;
            ListenToSlider();
        }

        public override void ApplyLayout(DSMPanelLayout layout)
        {
            base.ApplyLayout(layout);
            _decimals = layout.FloatDecimals;
            _format = "F" + _decimals.ToString(CultureInfo.InvariantCulture);
            if (_hasValue) Show(_current);
        }

        protected override void Show(float value)
        {
            _current = value;
            _hasValue = true;
            // The slider clamps its position to the Range; the input keeps the real Value.
            if (_slider != null) _slider.SetValueWithoutNotify(value);
            _input?.SetTextWithoutNotify(value.ToString(_format, CultureInfo.InvariantCulture));
        }

        // Registered only after min/max are set: those setters re-clamp the value with the callback on.
        private void ListenToSlider()
        {
            _slider!.onValueChanged.RemoveListener(OnSliderChanged);
            _slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float value) =>
            Commit((float)Math.Round(value, _decimals, MidpointRounding.AwayFromZero));

        private void OnEndEdit(string text)
        {
            if (float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                Commit(value);
            else
                Show(DSM.Get(Key, 0f));
        }
    }
}
