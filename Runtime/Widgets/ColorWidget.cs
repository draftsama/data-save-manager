#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class ColorWidget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private TMP_InputField? _rInput;
    [SerializeField] private TMP_InputField? _gInput;
    [SerializeField] private TMP_InputField? _bInput;
    [SerializeField] private TMP_InputField? _aInput;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        if (_label == null || _rInput == null || _gInput == null || _bInput == null || _aInput == null)
        {
            Debug.LogError($"ColorWidget on '{gameObject.name}': _label, _rInput, _gInput, _bInput, or _aInput is not assigned.", this);
            return;
        }
        _key = key;
        _slot = slot;
        _label.text = label;

        var value = slot.Get(key, Color.white);
        _rInput.text = value.r.ToString("G", CultureInfo.InvariantCulture);
        _gInput.text = value.g.ToString("G", CultureInfo.InvariantCulture);
        _bInput.text = value.b.ToString("G", CultureInfo.InvariantCulture);
        _aInput.text = value.a.ToString("G", CultureInfo.InvariantCulture);

        _rInput.onEndEdit.AddListener(_ => ApplyValue());
        _gInput.onEndEdit.AddListener(_ => ApplyValue());
        _bInput.onEndEdit.AddListener(_ => ApplyValue());
        _aInput.onEndEdit.AddListener(_ => ApplyValue());
    }

    private void ApplyValue()
    {
        float.TryParse(_rInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var r);
        float.TryParse(_gInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var g);
        float.TryParse(_bInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var b);
        float.TryParse(_aInput?.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var a);
        _slot?.Set(_key, new Color(r, g, b, a));
    }
}
