#nullable enable

using TMPro;
using UnityEngine;

namespace OperatorConsole
{
    public sealed class StringWidget : OCWidget<string>
    {
        [SerializeField] private TMP_InputField? _input;

        private void Awake()
        {
            if (_input != null) _input.onEndEdit.AddListener(Commit);
        }

        protected override void Show(string value) => _input?.SetTextWithoutNotify(value);
    }
}
