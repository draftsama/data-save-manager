#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class ColorWidget : DSMWidget<Color>
    {
        [SerializeField] private TMP_InputField? _hexInput;
        [SerializeField] private Image? _swatch;

        private void Awake()
        {
            if (_hexInput != null) _hexInput.onEndEdit.AddListener(_ => Apply());
        }

        protected override void Show(Color value)
        {
            var hex = "#" + (value.a >= 1f
                ? ColorUtility.ToHtmlStringRGB(value)
                : ColorUtility.ToHtmlStringRGBA(value));
            if (_hexInput != null) _hexInput.SetTextWithoutNotify(hex);
            if (_swatch != null) _swatch.color = value;
        }

        private void Apply()
        {
            var text = _hexInput != null ? _hexInput.text : string.Empty;
            var trimmed = text.Trim();
            if (trimmed.Length > 0 && trimmed[0] != '#') trimmed = "#" + trimmed;

            if (ColorUtility.TryParseHtmlString(trimmed, out var color)) Commit(color);
            else Show(DSM.Get(Key, Color.white));
        }
    }
}
