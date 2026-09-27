#nullable enable

using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class StringWidget : DSMWidget<string>
    {
        [SerializeField] private TMP_InputField? _input;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(Commit);
        }

        protected override void Show(string value) => _input?.SetTextWithoutNotify(value);
    }
}
