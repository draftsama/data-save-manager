#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class Vector3Widget : OCWidget<Vector3>
    {
        private readonly FloatField _x = new() { isDelayed = true };
        private readonly FloatField _y = new() { isDelayed = true };
        private readonly FloatField _z = new() { isDelayed = true };

        public Vector3Widget()
        {
            _x.RegisterValueChangedCallback(_ => Apply());
            _y.RegisterValueChangedCallback(_ => Apply());
            _z.RegisterValueChangedCallback(_ => Apply());
            Field.Add(Axis("X"));
            Field.Add(Grow(_x));
            Field.Add(Axis("Y"));
            Field.Add(Grow(_y));
            Field.Add(Axis("Z"));
            Field.Add(Grow(_z));
        }

        protected override void Show(Vector3 value)
        {
            _x.SetValueWithoutNotify(value.x);
            _y.SetValueWithoutNotify(value.y);
            _z.SetValueWithoutNotify(value.z);
        }

        private void Apply() => Commit(new Vector3(_x.value, _y.value, _z.value));
    }
}
