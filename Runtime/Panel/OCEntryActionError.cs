#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    /// <summary>The line under a widget row that shows why its last Entry Action failed. Hidden while there is no error.</summary>
    public sealed class OCEntryActionError : MonoBehaviour, IOCLayoutAware
    {
        [SerializeField] private TMP_Text? _text;

        private const int RowPaddingAndSpacing = 12;

        public void SetError(string? message)
        {
            if (_text != null) _text.text = message ?? string.Empty;
            gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            if (_text != null) _text.fontSize = layout.EntryActionErrorFontSize;
            if (TryGetComponent<HorizontalOrVerticalLayoutGroup>(out var group))
            {
                var padding = group.padding;
                group.padding = new RectOffset(Mathf.RoundToInt(layout.LabelWidth) + RowPaddingAndSpacing, padding.right, padding.top, padding.bottom);
            }
        }
    }
}
