#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class IntWidget : DSMWidget<int>
    {
        [SerializeField] private TMP_InputField? _input;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(OnEndEdit);
        }

        protected override void Show(int value) => _input?.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));

        private void OnEndEdit(string text)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                Commit(value);
            else
                Show(DSM.Get(Key, 0));
        }
    }
}
