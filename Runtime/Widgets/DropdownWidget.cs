#nullable enable

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class DropdownWidget : OCWidget<int>
    {
        private readonly DropdownField _dropdown = new();
        private readonly List<string> _choices = new();
        private readonly List<int> _values = new();
        private bool _hasUnknownItem;

        public DropdownWidget()
        {
            _dropdown.choices = _choices;
            _dropdown.RegisterValueChangedCallback(_ => OnSelected(_dropdown.index));
            Field.Add(Grow(_dropdown));
        }

        protected override void OnSetup(OCEntryDefinition entry)
        {
            _values.Clear();
            _choices.Clear();
            _hasUnknownItem = false;
            foreach (var option in entry.Options)
            {
                _values.Add(option.Value);
                _choices.Add(option.Label);
            }
        }

        protected override void Show(int value)
        {
            var index = _values.IndexOf(value);
            if (index >= 0)
            {
                if (_hasUnknownItem)
                {
                    _choices.RemoveAt(_choices.Count - 1);
                    _hasUnknownItem = false;
                }
                _dropdown.SetValueWithoutNotify(_choices[index]);
                return;
            }

            // A Value matching no Option is kept as-is; the dropdown shows it as an extra item.
            var text = $"{value} (?)";
            if (_hasUnknownItem) _choices[_choices.Count - 1] = text;
            else
            {
                _choices.Add(text);
                _hasUnknownItem = true;
            }
            _dropdown.SetValueWithoutNotify(text);
        }

        private void OnSelected(int index)
        {
            if (index >= 0 && index < _values.Count) Commit(_values[index]);
        }
    }
}
