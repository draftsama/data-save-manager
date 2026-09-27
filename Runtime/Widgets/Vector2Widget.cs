#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class Vector2Widget : DSMWidget<Vector2>
    {
        [SerializeField] private TMP_InputField? _xInput;
        [SerializeField] private TMP_InputField? _yInput;

        private void Awake()
        {
            if (_xInput != null) _xInput.onEndEdit.AddListener(_ => Apply());
            if (_yInput != null) _yInput.onEndEdit.AddListener(_ => Apply());
        }

        protected override void Show(Vector2 value)
        {
            _xInput?.SetTextWithoutNotify(value.x.ToString(CultureInfo.InvariantCulture));
            _yInput?.SetTextWithoutNotify(value.y.ToString(CultureInfo.InvariantCulture));
        }

        private void Apply()
        {
            var xOk = TryParse(_xInput, out var x);
            var yOk = TryParse(_yInput, out var y);
            if (xOk && yOk) Commit(new Vector2(x, y));
            else Show(DSM.Get(Key, Vector2.zero));
        }

        private static bool TryParse(TMP_InputField? input, out float value) =>
            float.TryParse(input?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
