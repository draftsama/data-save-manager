#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The line under a widget row that shows why its last Entry Action failed. Hidden while there is no error.</summary>
    internal sealed class OCEntryActionError : Label, IOCLayoutAware
    {
        private const float RowPaddingAndSpacing = 24f;

        public OCEntryActionError()
        {
            AddToClassList("ocp-error");
            style.display = DisplayStyle.None;
        }

        public void SetError(string? message)
        {
            text = message ?? string.Empty;
            style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            style.fontSize = layout.EntryActionErrorFontSize;
            style.paddingLeft = layout.LabelWidth + RowPaddingAndSpacing;
        }
    }
}
