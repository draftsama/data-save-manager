#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class FloatWidget : DSMWidget<float>
    {
        [SerializeField] private TMP_InputField? _input;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(OnEndEdit);
        }

        protected override void Show(float value) => _input?.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));

        private void OnEndEdit(string text)
        {
            if (float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                Commit(value);
            else
                Show(DSM.Get(Key, 0f));
        }
    }
}
