#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class Vector2Widget : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;
        [SerializeField] private TMP_InputField? _xInput;
        [SerializeField] private TMP_InputField? _yInput;

        private string _key = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            if (_label == null || _xInput == null || _yInput == null)
            {
                Debug.LogError($"Vector2Widget on '{gameObject.name}': _label, _xInput, or _yInput is not assigned.", this);
                return;
            }
            _key = entry.Key;
            _label.text = entry.DisplayLabel;

            var value = DSM.Get(_key, Vector2.zero);
            _xInput.text = value.x.ToString("G", CultureInfo.InvariantCulture);
            _yInput.text = value.y.ToString("G", CultureInfo.InvariantCulture);

            _xInput.onEndEdit.AddListener(_ => ApplyValue());
            _yInput.onEndEdit.AddListener(_ => ApplyValue());
        }

        private void ApplyValue()
        {
            float.TryParse(_xInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            float.TryParse(_yInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var y);
            DSM.Set(_key, new Vector2(x, y));
        }
    }
}
