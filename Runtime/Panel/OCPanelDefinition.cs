#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The authored description of one Runtime Panel, kept in the Config. The app creates one Runtime Panel per enabled Panel Definition when it starts.</summary>
    [Serializable]
    public sealed class OCPanelDefinition
    {
        [SerializeField] private string _name = "Panel";
        [SerializeField] private bool _enabled = true;
        [SerializeField, Tooltip("UI Toolkit panel sort order; higher draws on top.")] private int _sortOrder = 1000;
        [SerializeField] private List<string> _audienceIds = new();
        [SerializeField, Tooltip("Auto shows the Save button only when AutoSave is off. Always / Never force it on / off.")]
        private OCSaveButtonMode _saveButtonMode = OCSaveButtonMode.Auto;
        [SerializeField, Tooltip("Open and close the panel with Toggle Key.")] private bool _hotkeyEnabled = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField, Tooltip("Open the panel by tapping one screen corner several times in quick succession.")]
        private OCCornerTap _cornerTap = new();
        [SerializeField] private OCPasscode _passcode = new();
        [SerializeField] private bool _startVisible;
        [SerializeField, Tooltip("Show the Log tab: messages the app wrote to Unity's log, kept from app start.")]
        private bool _showLog;
        [SerializeField, Tooltip("Show the Performance tab: frame rate, frame time and memory, measured from app start.")]
        private bool _showPerformance;
        [SerializeField] private OCPanelLayout _layout = new();

        public OCPanelDefinition() { }

        internal OCPanelDefinition(string name, bool startVisible = false)
        {
            _name = name;
            _hotkeyEnabled = false;
            _startVisible = startVisible;
        }

        public string Name => _name;
        public bool Enabled => _enabled;
        public int SortOrder => _sortOrder;

        /// <summary>Ids of the Audiences this Runtime Panel serves; empty means it shows every Exposed Entry.</summary>
        public IReadOnlyList<string> AudienceIds => _audienceIds;

        public OCSaveButtonMode SaveButtonMode => _saveButtonMode;
        public bool HotkeyEnabled => _hotkeyEnabled;
        public KeyCode ToggleKey => _toggleKey;
        public OCCornerTap CornerTap => _cornerTap;
        public OCPasscode Passcode => _passcode;
        public bool StartVisible => _startVisible;
        public bool ShowLog => _showLog;
        public bool ShowPerformance => _showPerformance;
        public OCPanelLayout Layout => _layout;
    }
}
