#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class IntWidget : OCWidget<int>
    {
        private readonly IntegerField _input = new() { isDelayed = true };

        public IntWidget()
        {
            _input.RegisterValueChangedCallback(e => Commit(e.newValue));
            Field.Add(Grow(_input));
        }

        protected override void Show(int value) => _input.SetValueWithoutNotify(value);
    }
}
