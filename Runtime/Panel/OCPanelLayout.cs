#nullable enable

using System;
using UnityEngine;

namespace OperatorConsole
{
    public enum OCPanelAnchor
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

    public enum OCPanelSizeMode
    {
        Fixed,
        FullScreen
    }

    public enum OCLabelOverflow
    {
        Ellipsis,
        Wrap
    }

    /// <summary>Look-and-feel settings of the Runtime Panel. A class (not a struct) so field initializers supply defaults to scene instances that predate the field.</summary>
    [Serializable]
    public sealed class OCPanelLayout
    {
        [SerializeField, Tooltip("Width of the label column in each row.")]
        private float _labelWidth = 220f;

        [SerializeField, Tooltip("Row height. With Label Overflow = Wrap this is the minimum; rows grow to fit the wrapped label.")]
        private float _rowHeight = 40f;

        [SerializeField, Tooltip("Vertical gap between rows.")]
        private float _rowSpacing = 4f;

        [SerializeField, Tooltip("Font size of every text in a row (label, input, placeholder, value).")]
        private float _fontSize = 20f;

        [SerializeField, Tooltip("Fixed uses Panel Size, Anchor and Offset; Full Screen stretches the panel over the whole screen.")]
        private OCPanelSizeMode _sizeMode = OCPanelSizeMode.Fixed;

        [SerializeField, Tooltip("Gap between the screen edges and a Full Screen panel.")]
        private float _fullScreenPadding;

        [SerializeField, Tooltip("Panel width and height (Fixed size mode).")]
        private Vector2 _panelSize = new(900f, 800f);

        [SerializeField, Tooltip("Screen point the panel is anchored and pivoted to.")]
        private OCPanelAnchor _anchor = OCPanelAnchor.Center;

        [SerializeField, Tooltip("Panel position relative to the anchor.")]
        private Vector2 _offset = Vector2.zero;

        [SerializeField, Tooltip("Panel background color.")]
        private Color _backgroundColor = new(0f, 0f, 0f, 0.98f);

        [SerializeField, Tooltip("Ellipsis keeps the label on one line; Wrap lets the row grow taller to fit it.")]
        private OCLabelOverflow _labelOverflow = OCLabelOverflow.Ellipsis;

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

        [SerializeField, Tooltip("Height of the Settings / Commands tabs.")]
        private float _tabHeight = 44f;

        [SerializeField, Tooltip("Font size of a Command's Description and error line.")]
        private float _commandDescriptionFontSize = 16f;

        [SerializeField, Tooltip("Width of a Command's Run button.")]
        private float _commandButtonWidth = 140f;

        [SerializeField, Tooltip("Width of each Entry Action button beside a widget; longer Titles end in an ellipsis.")]
        private float _entryActionButtonWidth = 120f;

        [SerializeField, Tooltip("Font size of the error line under a row whose Entry Action failed.")]
        private float _entryActionErrorFontSize = 16f;

        [SerializeField, Tooltip("Diameter of each Lock Screen key.")]
        private float _lockKeySize = 120f;

        [SerializeField, Tooltip("Gap between Lock Screen keys.")]
        private float _lockKeySpacing = 28f;

        [SerializeField, Tooltip("Font size of the Lock Screen key digits.")]
        private float _lockKeyFontSize = 48f;

        [SerializeField, Tooltip("Diameter of each Lock Screen passcode dot.")]
        private float _lockDotSize = 20f;

        [SerializeField, Range(0f, 1f), Tooltip("Opacity of the Lock Screen backdrop.")]
        private float _lockBackgroundAlpha = 0.9f;

        public OCPanelSizeMode SizeMode => _sizeMode;
        public float FullScreenPadding => _fullScreenPadding;
        public float LockKeySize => _lockKeySize;
        public float LockKeySpacing => _lockKeySpacing;
        public float LockKeyFontSize => _lockKeyFontSize;
        public float LockDotSize => _lockDotSize;
        public float LockBackgroundAlpha => Mathf.Clamp01(_lockBackgroundAlpha);
        public float LabelWidth => _labelWidth;
        public float RowHeight => _rowHeight;
        public float RowSpacing => _rowSpacing;
        public float FontSize => _fontSize;
        public Vector2 PanelSize => _panelSize;
        public OCPanelAnchor Anchor => _anchor;
        public Vector2 Offset => _offset;
        public Color BackgroundColor => _backgroundColor;
        public OCLabelOverflow LabelOverflow => _labelOverflow;
        public int FloatDecimals => Mathf.Clamp(_floatDecimals, 0, 6);
        public float ScrollbarWidth => _scrollbarWidth;
        public float ScrollSensitivity => _scrollSensitivity;
        public float HeaderFontSize => _headerFontSize;
        public float HeaderSpacing => _headerSpacing;
        public float TabHeight => _tabHeight;
        public float CommandDescriptionFontSize => _commandDescriptionFontSize;
        public float CommandButtonWidth => _commandButtonWidth;
        public float EntryActionButtonWidth => _entryActionButtonWidth;
        public float EntryActionErrorFontSize => _entryActionErrorFontSize;
    }
}
