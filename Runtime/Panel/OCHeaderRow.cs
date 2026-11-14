#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>A title row the Runtime Panel places directly above an Exposed Entry's widget row, or above a group of Commands.</summary>
    internal sealed class OCHeaderRow : Label, IOCLayoutAware
    {
        private const float HeaderScale = 0.62f;

        public OCHeaderRow(string text) : base(text.ToUpperInvariant())
        {
            AddToClassList("ocp-header");
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            style.fontSize = layout.HeaderFontSize * HeaderScale;
            style.marginTop = layout.HeaderSpacing;
        }
    }
}
