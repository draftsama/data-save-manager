#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class DoubleWidget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private TMP_InputField? _input;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        if (_label == null || _input == null)
        {
            Debug.LogError($"DoubleWidget on '{gameObject.name}': _label or _input is not assigned.", this);
            return;
        }
        _key = key;
        _slot = slot;
        _label.text = label;
        _input.text = slot.Get(key, 0.0).ToString("G", CultureInfo.InvariantCulture);
        _input.onEndEdit.AddListener(_ => Apply());
    }

    private void Apply()
    {
        if (double.TryParse(_input?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            _slot?.Set(_key, v);
    }
}
