#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    /// <summary>A title row the Runtime Panel places directly above an Exposed Entry's widget row.</summary>
    public sealed class DSMHeaderRow : MonoBehaviour, IDSMLayoutAware
    {
        [SerializeField] private TMP_Text? _text;

        public void SetText(string text)
        {
            if (_text != null) _text.text = text;
        }

        public void ApplyLayout(DSMPanelLayout layout)
        {
            if (_text != null) _text.fontSize = layout.HeaderFontSize;

            if (TryGetComponent<VerticalLayoutGroup>(out var group))
            {
                // A new RectOffset: assigning the same instance back would not mark the layout dirty.
                var padding = group.padding;
                group.padding = new RectOffset(padding.left, padding.right, Mathf.RoundToInt(layout.HeaderSpacing), padding.bottom);
            }
        }
    }
}
