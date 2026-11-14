#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class BoolWidget : OCWidget<bool>
    {
        private const float SwitchWidth = 52f;

        private readonly Toggle _toggle = new();

        public BoolWidget()
        {
            _toggle.AddToClassList("ocp-switch");
            var knob = new VisualElement();
            knob.AddToClassList("ocp-switch-knob");
            _toggle.Q(className: Toggle.checkmarkUssClassName).Add(knob);
            // The default theme gives the input flex-basis 0, which beats width once it stops growing.
            var input = _toggle.Q(className: Toggle.inputUssClassName);
            input.style.flexBasis = StyleKeyword.Auto;
            input.style.width = SwitchWidth;
            input.style.flexShrink = 0f;
            _toggle.RegisterValueChangedCallback(e => Commit(e.newValue));
            Field.Add(_toggle);
        }

        protected override void Show(bool value) => _toggle.SetValueWithoutNotify(value);
    }
}
