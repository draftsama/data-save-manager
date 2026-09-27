#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class BoolWidget : DSMWidget<bool>
    {
        [SerializeField] private Toggle? _toggle;

        private void Awake()
        {
            if (_toggle != null) _toggle.onValueChanged.AddListener(Commit);
        }

        protected override void Show(bool value) => _toggle?.SetIsOnWithoutNotify(value);
    }
}
