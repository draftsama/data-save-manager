#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class Vector3Widget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private TMP_InputField? _xInput;
        [SerializeField] private TMP_InputField? _yInput;
        [SerializeField] private TMP_InputField? _zInput;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _xInput == null || _yInput == null || _zInput == null)
            {
                Debug.LogError($"Vector3Widget on '{gameObject.name}': _label, _xInput, _yInput, or _zInput is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;

            var value = DSM.Get(_key, Vector3.zero);
            _xInput.text = value.x.ToString("G", CultureInfo.InvariantCulture);
            _yInput.text = value.y.ToString("G", CultureInfo.InvariantCulture);
            _zInput.text = value.z.ToString("G", CultureInfo.InvariantCulture);

            _xInput.onEndEdit.AddListener(_ => ApplyValue());
            _yInput.onEndEdit.AddListener(_ => ApplyValue());
            _zInput.onEndEdit.AddListener(_ => ApplyValue());
        }

        private void ApplyValue()
        {
            float.TryParse(_xInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            float.TryParse(_yInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var y);
            float.TryParse(_zInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var z);
            DSM.Set(_key, new Vector3(x, y, z));
        }
    }
}
