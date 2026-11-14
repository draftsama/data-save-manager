#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>One Metric on the Performance tab: name, value (or n/a), an optional note and an optional fill bar.</summary>
    internal sealed class OCMetricCell : VisualElement, IOCLayoutAware
    {
        private readonly Label _name;
        private readonly Label _value = new();
        private readonly Label _note = new();
        private readonly VisualElement? _bar;
        private readonly VisualElement? _fill;
        private string? _shownValue;
        private string? _shownNote;

        public OCMetricCell(string name, bool withBar = false)
        {
            AddToClassList("ocp-metric");
            _name = new Label(name);
            _name.AddToClassList("ocp-metric__name");
            _value.AddToClassList("ocp-metric__value");
            _note.AddToClassList("ocp-metric__note");
            Add(_name);
            Add(_value);
            if (withBar)
            {
                _bar = new VisualElement();
                _bar.AddToClassList("ocp-metric__bar");
                _fill = new VisualElement();
                _fill.AddToClassList("ocp-metric__fill");
                _bar.Add(_fill);
                Add(_bar);
            }
            Add(_note);
            _note.style.display = DisplayStyle.None;
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            _name.style.fontSize = layout.FontSize * 0.7f;
            _value.style.fontSize = layout.FontSize;
            _note.style.fontSize = layout.FontSize * 0.65f;
        }

        public void SetValue(string value, bool over = false)
        {
            if (value != _shownValue)
            {
                _shownValue = value;
                _value.text = value;
                _value.EnableInClassList("ocp-metric__value--na", value == "n/a");
            }
            _value.EnableInClassList("ocp-metric__value--over", over);
        }

        public void SetNote(string? note)
        {
            if (note == _shownNote) return;
            _shownNote = note;
            _note.text = note ?? string.Empty;
            _note.style.display = note != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetFill(float fraction)
        {
            if (_fill != null) _fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }
    }
}
