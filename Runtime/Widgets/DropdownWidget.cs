#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class DropdownWidget : DSMWidget<int>
    {
        [SerializeField] private TMP_Dropdown? _dropdown;

        private readonly List<int> _values = new();
        private bool _hasUnknownItem;

        private void Awake()
        {
            if (_dropdown != null) _dropdown.onValueChanged.AddListener(OnSelected);
        }

        protected override void OnSetup(DSMEntryDefinition entry)
        {
            if (_dropdown == null) return;

            _values.Clear();
            _dropdown.ClearOptions();
            _hasUnknownItem = false;

            foreach (var option in entry.Options)
            {
                _values.Add(option.Value);
                _dropdown.options.Add(new TMP_Dropdown.OptionData(option.Label));
            }
        }

        protected override void Show(int value)
        {
            if (_dropdown == null) return;

            var index = _values.IndexOf(value);
            if (index >= 0)
            {
                if (_hasUnknownItem)
                {
                    _dropdown.options.RemoveAt(_dropdown.options.Count - 1);
                    _hasUnknownItem = false;
                }
                _dropdown.SetValueWithoutNotify(index);
                _dropdown.RefreshShownValue();
                return;
            }

            // A Value matching no Option is kept as-is; the dropdown shows it as an extra, unselectable-by-intent item.
            var text = $"{value} (?)";
            if (_hasUnknownItem)
            {
                _dropdown.options[_dropdown.options.Count - 1].text = text;
            }
            else
            {
                _dropdown.options.Add(new TMP_Dropdown.OptionData(text));
                _hasUnknownItem = true;
            }
            _dropdown.SetValueWithoutNotify(_values.Count);
            _dropdown.RefreshShownValue();
        }

        private void OnSelected(int index)
        {
            if (index >= 0 && index < _values.Count) Commit(_values[index]);
        }
    }
}
