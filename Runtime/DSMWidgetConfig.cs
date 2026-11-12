#nullable enable

using System;
using UnityEngine;

namespace DataSaveManager
{
    [CreateAssetMenu(menuName = "DSM/Widget Config")]
    public sealed class DSMWidgetConfig : ScriptableObject
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
        [SerializeField] private DSMHeaderRow? _headerRow;

        public DSMHeaderRow? HeaderRow => _headerRow;

        public MonoBehaviour? GetWidgetPrefab(DSMDataType type) => type switch
        {
            DSMDataType.Bool    => _boolWidget,
            DSMDataType.Int     => _intWidget,
            DSMDataType.Float   => _floatWidget,
            DSMDataType.String  => _stringWidget,
            DSMDataType.Vector2 => _vector2Widget,
            DSMDataType.Vector3 => _vector3Widget,
            DSMDataType.Color   => _colorWidget,
            _                   => null
        };

        [Obsolete("Use DSMPresentation to pick the widget kind; removed in 3.0.")]
        public MonoBehaviour? GetWidgetPrefab(DSMEntryDefinition entry) =>
            GetWidgetPrefab(DSMPresentation.Present(DSMHintSettings.From(entry)).Widget, entry.Type);

        /// <summary>The prefab for <paramref name="kind"/>, falling back to the plain prefab for <paramref name="type"/> when that slot is empty.</summary>
        internal MonoBehaviour? GetWidgetPrefab(DSMWidgetKind kind, DSMDataType type)
        {
            MonoBehaviour? special = kind switch
            {
                DSMWidgetKind.Dropdown => _dropdownWidget,
                DSMWidgetKind.Slider when type == DSMDataType.Int => _intSliderWidget,
                DSMWidgetKind.Slider when type == DSMDataType.Float => _floatSliderWidget,
                _ => null
            };
            return special != null ? special : GetWidgetPrefab(type);
        }
    }
}
