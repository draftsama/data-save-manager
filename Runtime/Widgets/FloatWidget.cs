#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class FloatWidget : OCWidget<float>
    {
        private readonly FloatField _input = new() { isDelayed = true };

        public FloatWidget()
        {
            _input.RegisterValueChangedCallback(e => Commit(e.newValue));
            Field.Add(Grow(_input));
        }

        protected override void Show(float value) => _input.SetValueWithoutNotify(value);
    }
}
