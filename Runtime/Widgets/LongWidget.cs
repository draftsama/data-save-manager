#nullable enable

using TMPro;
using UnityEngine;

public sealed class LongWidget : MonoBehaviour, IDSMWidget
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
        _input!.text = slot.Get(key, 0L).ToString();
        _input.onEndEdit.AddListener(_ => Apply());
    }

    private void Apply()
    {
        if (long.TryParse(_input!.text, out var v))
            _slot!.Set(_key, v);
    }
}
