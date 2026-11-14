#nullable enable

using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The row templates a Runtime Panel clones for each Entry, Header, Command and Entry Action.</summary>
    internal sealed class OCWidgetTemplates
    {
        public BoolWidget? BoolWidget { get; set; }
        public IntWidget? IntWidget { get; set; }
        public FloatWidget? FloatWidget { get; set; }
        public StringWidget? StringWidget { get; set; }
        public Vector2Widget? Vector2Widget { get; set; }
        public Vector3Widget? Vector3Widget { get; set; }
        public ColorWidget? ColorWidget { get; set; }
        public IntSliderWidget? IntSliderWidget { get; set; }
        public FloatSliderWidget? FloatSliderWidget { get; set; }
        public DropdownWidget? DropdownWidget { get; set; }
        public OCHeaderRow? HeaderRow { get; set; }
        public OCCommandRow? CommandRow { get; set; }
        public OCEntryActionBar? EntryActionBar { get; set; }
        public OCEntryActionError? EntryActionError { get; set; }

        public MonoBehaviour? GetWidget(OCDataType type) => type switch
        {
            OCDataType.Bool    => BoolWidget,
            OCDataType.Int     => IntWidget,
            OCDataType.Float   => FloatWidget,
            OCDataType.String  => StringWidget,
            OCDataType.Vector2 => Vector2Widget,
            OCDataType.Vector3 => Vector3Widget,
            OCDataType.Color   => ColorWidget,
            _                   => null
        };

        /// <summary>The template for <paramref name="kind"/>, falling back to the plain template for <paramref name="type"/> when that slot is empty.</summary>
        public MonoBehaviour? GetWidget(OCWidgetKind kind, OCDataType type)
        {
            MonoBehaviour? special = kind switch
            {
                OCWidgetKind.Dropdown => DropdownWidget,
                OCWidgetKind.Slider when type == OCDataType.Int => IntSliderWidget,
                OCWidgetKind.Slider when type == OCDataType.Float => FloatSliderWidget,
                _ => null
            };
            return special != null ? special : GetWidget(type);
        }
    }
}
