#nullable enable

namespace OperatorConsole
{
    /// <summary>Picks the built-in widget row for an Entry.</summary>
    internal static class OCWidgets
    {
        /// <summary>A new row for <paramref name="kind"/>, falling back to the plain widget for <paramref name="type"/> when that kind does not apply to it; null for an unknown type.</summary>
        public static OCWidgetRow? Create(OCWidgetKind kind, OCDataType type) => (kind, type) switch
        {
            (OCWidgetKind.Dropdown, OCDataType.Int) => new DropdownWidget(),
            (OCWidgetKind.Slider, OCDataType.Int)   => new IntSliderWidget(),
            (OCWidgetKind.Slider, OCDataType.Float) => new FloatSliderWidget(),
            (_, OCDataType.Bool)    => new BoolWidget(),
            (_, OCDataType.Int)     => new IntWidget(),
            (_, OCDataType.Float)   => new FloatWidget(),
            (_, OCDataType.String)  => new StringWidget(),
            (_, OCDataType.Vector2) => new Vector2Widget(),
            (_, OCDataType.Vector3) => new Vector3Widget(),
            (_, OCDataType.Color)   => new ColorWidget(),
            _                       => null
        };
    }
}
