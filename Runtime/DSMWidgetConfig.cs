#nullable enable

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
    }
}
