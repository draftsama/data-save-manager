#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class IntSliderWidget : DSMWidget<int>
    {
        [SerializeField] private Slider? _slider;
        [SerializeField] private TMP_InputField? _input;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(OnEndEdit);
        }

        protected override void OnSetup(DSMEntryDefinition entry)
        {
            if (_slider == null) return;
            _slider.wholeNumbers = true;
            _slider.minValue = Mathf.Ceil(entry.RangeMin);
            _slider.maxValue = Mathf.Floor(entry.RangeMax);
            ListenToSlider();
        }

        protected override void Show(int value)
        {
            // The slider clamps its position to the Range; the input keeps the real Value.
            if (_slider != null) _slider.SetValueWithoutNotify(value);
            _input?.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
        }

        // Registered only after min/max are set: those setters re-clamp the value with the callback on.
        private void ListenToSlider()
        {
            _slider!.onValueChanged.RemoveListener(OnSliderChanged);
            _slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float value) => Commit(Mathf.RoundToInt(value));

        private void OnEndEdit(string text)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                Commit(value);
            else
                Show(DSM.Get(Key, 0));
        }
    }
}
