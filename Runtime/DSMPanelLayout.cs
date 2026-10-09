#nullable enable

using System;
using UnityEngine;

namespace DataSaveManager
{
    public enum DSMPanelAnchor
    {
        Center,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public enum DSMLabelOverflow
    {
        Ellipsis,
        Wrap
    }

    /// <summary>Look-and-feel settings of the Runtime Panel. A class (not a struct) so field initializers supply defaults to scene instances that predate the field.</summary>
    [Serializable]
    public sealed class DSMPanelLayout
    {
        [SerializeField, Tooltip("Width of the label column in each row.")]
        private float _labelWidth = 220f;

        [SerializeField, Tooltip("Row height. With Label Overflow = Wrap this is the minimum; rows grow to fit the wrapped label.")]
        private float _rowHeight = 40f;

        [SerializeField, Tooltip("Vertical gap between rows.")]
        private float _rowSpacing = 4f;

        [SerializeField, Tooltip("Font size of every text in a row (label, input, placeholder, value).")]
        private float _fontSize = 20f;

        [SerializeField, Tooltip("Panel width and height.")]
        private Vector2 _panelSize = new(900f, 800f);

        [SerializeField, Tooltip("Screen point the panel is anchored and pivoted to.")]
        private DSMPanelAnchor _anchor = DSMPanelAnchor.Center;

        [SerializeField, Tooltip("Panel position relative to the anchor.")]
        private Vector2 _offset = Vector2.zero;

        [SerializeField, Tooltip("Panel background color.")]
        private Color _backgroundColor = new(0.08f, 0.08f, 0.1f, 0.92f);

        [SerializeField, Tooltip("Ellipsis keeps the label on one line; Wrap lets the row grow taller to fit it.")]
        private DSMLabelOverflow _labelOverflow = DSMLabelOverflow.Ellipsis;

        [SerializeField, Range(0, 6), Tooltip("Decimal places shown by float sliders and used when rounding slider commits.")]
        private int _floatDecimals = 2;

        [SerializeField, Tooltip("Width of the vertical scrollbar.")]
        private float _scrollbarWidth = 20f;

        [SerializeField, Tooltip("Mouse-wheel scroll speed.")]
        private float _scrollSensitivity = 30f;

        [SerializeField, Tooltip("Font size of Entry headers.")]
        private float _headerFontSize = 24f;

        [SerializeField, Tooltip("Extra space above each Entry header.")]
        private float _headerSpacing = 12f;

        public float LabelWidth => _labelWidth;
        public float RowHeight => _rowHeight;
        public float RowSpacing => _rowSpacing;
        public float FontSize => _fontSize;
        public Vector2 PanelSize => _panelSize;
        public DSMPanelAnchor Anchor => _anchor;
        public Vector2 Offset => _offset;
        public Color BackgroundColor => _backgroundColor;
        public DSMLabelOverflow LabelOverflow => _labelOverflow;
        public int FloatDecimals => Mathf.Clamp(_floatDecimals, 0, 6);
        public float ScrollbarWidth => _scrollbarWidth;
        public float ScrollSensitivity => _scrollSensitivity;
        public float HeaderFontSize => _headerFontSize;
        public float HeaderSpacing => _headerSpacing;
    }
}
