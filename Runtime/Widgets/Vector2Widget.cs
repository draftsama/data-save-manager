#nullable enable

using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class Vector2Widget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private TMP_InputField? _xInput;
    [SerializeField] private TMP_InputField? _yInput;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        _key = key;
        _slot = slot;
        _label!.text = label;

        var value = slot.Get(key, Vector2.zero);
        _xInput!.text = value.x.ToString("G", CultureInfo.InvariantCulture);
        _yInput!.text = value.y.ToString("G", CultureInfo.InvariantCulture);

        _xInput.onEndEdit.AddListener(_ => ApplyValue());
        _yInput.onEndEdit.AddListener(_ => ApplyValue());
    }

    private void ApplyValue()
    {
        float.TryParse(_xInput!.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
        float.TryParse(_yInput!.text, NumberStyles.Any, CultureInfo.InvariantCulture, out var y);
        _slot!.Set(_key, new Vector2(x, y));
    }
}
