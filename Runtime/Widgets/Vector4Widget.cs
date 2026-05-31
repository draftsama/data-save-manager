#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class Vector4Widget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private TMP_InputField? _xInput;
    [SerializeField] private TMP_InputField? _yInput;
    [SerializeField] private TMP_InputField? _zInput;
    [SerializeField] private TMP_InputField? _wInput;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        if (_label == null || _xInput == null || _yInput == null || _zInput == null || _wInput == null)
        {
            Debug.LogError($"Vector4Widget on '{gameObject.name}': _label, _xInput, _yInput, _zInput, or _wInput is not assigned.", this);
            return;
        }
        _key = key;
        _slot = slot;
        _label.text = label;

        var value = slot.Get(key, Vector4.zero);
        _xInput.text = value.x.ToString("G", CultureInfo.InvariantCulture);
        _yInput.text = value.y.ToString("G", CultureInfo.InvariantCulture);
        _zInput.text = value.z.ToString("G", CultureInfo.InvariantCulture);
        _wInput.text = value.w.ToString("G", CultureInfo.InvariantCulture);

        _xInput.onEndEdit.AddListener(_ => ApplyValue());
        _yInput.onEndEdit.AddListener(_ => ApplyValue());
        _zInput.onEndEdit.AddListener(_ => ApplyValue());
        _wInput.onEndEdit.AddListener(_ => ApplyValue());
    }

    private void ApplyValue()
    {
        float.TryParse(_xInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
        float.TryParse(_yInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var y);
        float.TryParse(_zInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var z);
        float.TryParse(_wInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var w);
        _slot?.Set(_key, new Vector4(x, y, z, w));
    }
}
