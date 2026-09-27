#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class ColorWidget : DSMWidget<Color>
    {
        [SerializeField] private TMP_InputField? _rInput;
        [SerializeField] private TMP_InputField? _gInput;
        [SerializeField] private TMP_InputField? _bInput;
        [SerializeField] private TMP_InputField? _aInput;
        [SerializeField] private Image? _swatch;

        private void Awake()
        {
            if (_rInput != null) _rInput.onEndEdit.AddListener(_ => Apply());
            if (_gInput != null) _gInput.onEndEdit.AddListener(_ => Apply());
            if (_bInput != null) _bInput.onEndEdit.AddListener(_ => Apply());
            if (_aInput != null) _aInput.onEndEdit.AddListener(_ => Apply());
        }

        protected override void Show(Color value)
        {
            _rInput?.SetTextWithoutNotify(value.r.ToString(CultureInfo.InvariantCulture));
            _gInput?.SetTextWithoutNotify(value.g.ToString(CultureInfo.InvariantCulture));
            _bInput?.SetTextWithoutNotify(value.b.ToString(CultureInfo.InvariantCulture));
            _aInput?.SetTextWithoutNotify(value.a.ToString(CultureInfo.InvariantCulture));
            if (_swatch != null) _swatch.color = value;
        }

        private void Apply()
        {
            var rOk = TryParse(_rInput, out var r);
            var gOk = TryParse(_gInput, out var g);
            var bOk = TryParse(_bInput, out var b);
            var aOk = TryParse(_aInput, out var a);
            if (rOk && gOk && bOk && aOk) Commit(new Color(r, g, b, a));
            else Show(DSM.Get(Key, Color.white));
        }

        private static bool TryParse(TMP_InputField? input, out float value) =>
            float.TryParse(input?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
