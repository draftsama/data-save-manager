#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    public sealed class BoolWidget : OCWidget<bool>
    {
        [SerializeField] private Toggle? _toggle;

        private void Awake()
        {
            if (_toggle != null) _toggle.onValueChanged.AddListener(Commit);
        }

        protected override void Show(bool value) => _toggle?.SetIsOnWithoutNotify(value);
    }
}
