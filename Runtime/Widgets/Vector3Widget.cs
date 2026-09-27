#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class Vector3Widget : DSMWidget<Vector3>
    {
        [SerializeField] private TMP_InputField? _xInput;
        [SerializeField] private TMP_InputField? _yInput;
        [SerializeField] private TMP_InputField? _zInput;

        private void Awake()
        {
            if (_xInput != null) _xInput.onEndEdit.AddListener(_ => Apply());
            if (_yInput != null) _yInput.onEndEdit.AddListener(_ => Apply());
            if (_zInput != null) _zInput.onEndEdit.AddListener(_ => Apply());
        }

        protected override void Show(Vector3 value)
        {
            _xInput?.SetTextWithoutNotify(value.x.ToString(CultureInfo.InvariantCulture));
            _yInput?.SetTextWithoutNotify(value.y.ToString(CultureInfo.InvariantCulture));
            _zInput?.SetTextWithoutNotify(value.z.ToString(CultureInfo.InvariantCulture));
        }

        private void Apply()
        {
            var xOk = TryParse(_xInput, out var x);
            var yOk = TryParse(_yInput, out var y);
            var zOk = TryParse(_zInput, out var z);
            if (xOk && yOk && zOk) Commit(new Vector3(x, y, z));
            else Show(DSM.Get(Key, Vector3.zero));
        }

        private static bool TryParse(TMP_InputField? input, out float value) =>
            float.TryParse(input?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
