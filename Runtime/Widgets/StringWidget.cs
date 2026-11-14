#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class StringWidget : OCWidget<string>
    {
        private readonly TextField _input = new() { isDelayed = true };

        public StringWidget()
        {
            _input.RegisterValueChangedCallback(e => Commit(e.newValue));
            Field.Add(Grow(_input));
        }

        protected override void Show(string value) => _input.SetValueWithoutNotify(value);
    }
}
