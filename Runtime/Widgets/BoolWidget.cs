#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BoolWidget : MonoBehaviour, IDSMWidget
{
    [SerializeField] private TextMeshProUGUI? _label;
    [SerializeField] private Toggle? _toggle;

    private string _key = string.Empty;
    private DSMSlot? _slot;

    public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
    {
        if (_label == null || _toggle == null)
        {
            Debug.LogError($"BoolWidget on '{gameObject.name}': _label or _toggle is not assigned.", this);
            return;
        }
        _key = key;
        _slot = slot;
        _label.text = label;
        _toggle.SetIsOnWithoutNotify(slot.Get(key, false));
        _toggle.onValueChanged.AddListener(value => _slot?.Set(_key, value));
    }
}
