#nullable enable

using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class IntWidget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private TMP_InputField? _input;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _input == null)
            {
                Debug.LogError($"IntWidget on '{gameObject.name}': _label or _input is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;
            _input.text = DSM.Get(_key, 0).ToString();
            _input.onEndEdit.AddListener(_ => Apply());
        }

        private void Apply()
        {
            if (int.TryParse(_input?.text, out var v))
                DSM.Set(_key, v);
        }
    }
}
