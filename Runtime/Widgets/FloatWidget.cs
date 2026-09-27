#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class FloatWidget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private TMP_InputField? _input;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _input == null)
            {
                Debug.LogError($"FloatWidget on '{gameObject.name}': _label or _input is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;
            _input.text = DSM.Get(_key, 0f).ToString("G", CultureInfo.InvariantCulture);
            _input.onEndEdit.AddListener(_ => Apply());
        }

        private void Apply()
        {
            if (float.TryParse(_input?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                DSM.Set(_key, v);
        }
    }
}
