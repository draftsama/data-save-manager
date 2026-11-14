#nullable enable

using System;
using UnityEngine;

namespace OperatorConsole
{
    [CreateAssetMenu(menuName = "Operator Console/Widget Config")]
    public sealed class OCWidgetConfig : ScriptableObject
    {
        [SerializeField] private BoolWidget? _boolWidget;
        [SerializeField] private IntWidget? _intWidget;
        [SerializeField] private FloatWidget? _floatWidget;
        [SerializeField] private StringWidget? _stringWidget;
        [SerializeField] private Vector2Widget? _vector2Widget;
        [SerializeField] private Vector3Widget? _vector3Widget;
        [SerializeField] private ColorWidget? _colorWidget;
        [SerializeField] private IntSliderWidget? _intSliderWidget;
        [SerializeField] private FloatSliderWidget? _floatSliderWidget;
        [SerializeField] private DropdownWidget? _dropdownWidget;
        [SerializeField] private OCHeaderRow? _headerRow;
        [SerializeField] private OCCommandRow? _commandRow;
        [SerializeField] private OCEntryActionBar? _entryActionBar;
        [SerializeField] private OCEntryActionError? _entryActionError;

        public OCHeaderRow? HeaderRow => _headerRow;
        public OCCommandRow? CommandRow => _commandRow;
        public OCEntryActionBar? EntryActionBar => _entryActionBar;
        public OCEntryActionError? EntryActionError => _entryActionError;

        public MonoBehaviour? GetWidgetPrefab(OCDataType type) => type switch
        {
            OCDataType.Bool    => _boolWidget,
            OCDataType.Int     => _intWidget,
            OCDataType.Float   => _floatWidget,
            OCDataType.String  => _stringWidget,
            OCDataType.Vector2 => _vector2Widget,
            OCDataType.Vector3 => _vector3Widget,
            OCDataType.Color   => _colorWidget,
            _                   => null
        };

        [Obsolete("Use OCPresentation to pick the widget kind; removed in 3.0.")]
        public MonoBehaviour? GetWidgetPrefab(OCEntryDefinition entry) =>
            GetWidgetPrefab(OCPresentation.Present(OCHintSettings.From(entry)).Widget, entry.Type);

        /// <summary>The prefab for <paramref name="kind"/>, falling back to the plain prefab for <paramref name="type"/> when that slot is empty.</summary>
        internal MonoBehaviour? GetWidgetPrefab(OCWidgetKind kind, OCDataType type)
        {
            MonoBehaviour? special = kind switch
            {
                OCWidgetKind.Dropdown => _dropdownWidget,
                OCWidgetKind.Slider when type == OCDataType.Int => _intSliderWidget,
                OCWidgetKind.Slider when type == OCDataType.Float => _floatSliderWidget,
                _ => null
            };
            return special != null ? special : GetWidgetPrefab(type);
        }
    }
}
