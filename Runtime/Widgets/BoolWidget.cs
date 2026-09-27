#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    public sealed class BoolWidget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private Toggle? _toggle;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _toggle == null)
            {
                Debug.LogError($"BoolWidget on '{gameObject.name}': _label or _toggle is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;
            _toggle.SetIsOnWithoutNotify(DSM.Get(_key, false));
            _toggle.onValueChanged.AddListener(value => DSM.Set(_key, value));
        }
    }
}
