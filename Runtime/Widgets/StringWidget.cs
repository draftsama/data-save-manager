#nullable enable

using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class StringWidget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private TMP_InputField? _input;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _input == null)
            {
                Debug.LogError($"StringWidget on '{gameObject.name}': _label or _input is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;
            _input.text = DSM.Get(_key, string.Empty);
            _input.onEndEdit.AddListener(value => DSM.Set(_key, value));
        }
    }
}
