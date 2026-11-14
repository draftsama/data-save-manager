#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class Vector2Widget : OCWidget<Vector2>
    {
        private readonly FloatField _x = new() { isDelayed = true };
        private readonly FloatField _y = new() { isDelayed = true };

        public Vector2Widget()
        {
            _x.RegisterValueChangedCallback(_ => Apply());
            _y.RegisterValueChangedCallback(_ => Apply());
            Field.Add(Axis("X"));
            Field.Add(Grow(_x));
            Field.Add(Axis("Y"));
            Field.Add(Grow(_y));
        }

        protected override void Show(Vector2 value)
        {
            _x.SetValueWithoutNotify(value.x);
            _y.SetValueWithoutNotify(value.y);
        }

        private void Apply() => Commit(new Vector2(_x.value, _y.value));
    }
}
