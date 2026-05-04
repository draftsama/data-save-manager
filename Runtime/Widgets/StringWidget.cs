#nullable enable

using TMPro;
using UnityEngine;

public sealed class StringWidget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private TMP_InputField? _input;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        _key = key;
        _slot = slot;
        _label!.text = label;
        _input!.text = slot.Get(key, string.Empty);
        _input.onEndEdit.AddListener(value => _slot.Set(_key, value));
    }
}
