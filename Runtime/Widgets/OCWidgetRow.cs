#nullable enable

using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>One Runtime Panel row for an Exposed Entry: a label, then the widget's controls in <see cref="Field"/>.</summary>
    internal abstract class OCWidgetRow : VisualElement, IOCLayoutAware
    {
        private const float InputInset = 6f;

        private readonly Label _label;

        protected OCWidgetRow()
        {
            AddToClassList("ocp-row");
            _label = new Label();
            _label.AddToClassList("ocp-label");
            Add(_label);
            Field = new VisualElement();
            Field.AddToClassList("ocp-field");
            Add(Field);
        }

        /// <summary>The part of the row right of the label, where the widget and its Entry Action buttons go.</summary>
        public VisualElement Field { get; }

        /// <summary>Shows <paramref name="entry"/> and commits edits to it through <paramref name="store"/>.</summary>
        public abstract void Setup(OCEntryDefinition entry, OCStore store);

        protected void SetLabel(string text)
        {
            _label.text = text;
            _label.tooltip = text;
        }

        public virtual void ApplyLayout(OCPanelLayout layout)
        {
            var wrap = layout.LabelOverflow == OCLabelOverflow.Wrap;
            style.minHeight = layout.RowHeight;
            style.height = wrap ? StyleKeyword.Auto : layout.RowHeight;
            style.fontSize = layout.FontSize;

            _label.style.width = layout.LabelWidth;
            _label.EnableInClassList("ocp-label--wrap", wrap);
            _label.EnableInClassList("ocp-label--ellipsis", !wrap);

            var inputHeight = layout.RowHeight - InputInset;
            this.Query(className: "unity-base-text-field__input").ForEach(input => input.style.height = inputHeight);
            this.Query(className: "unity-base-popup-field__input").ForEach(input => input.style.height = inputHeight);
        }

        protected static TField Grow<TField>(TField field) where TField : VisualElement
        {
            field.AddToClassList("ocp-grow");
            return field;
        }

        protected static Label Axis(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ocp-axis");
            return label;
        }
    }
}
