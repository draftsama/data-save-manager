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
        [SerializeField, Tooltip("Panel prefab this Runtime Panel is created from.")] private OCRuntimePanel? _prefab;
        [SerializeField, Tooltip("Canvas sort order; higher draws on top.")] private int _sortOrder = 1000;
        [SerializeField] private List<string> _audienceIds = new();
        [SerializeField, Tooltip("Auto shows the Save button only when AutoSave is off. Always / Never force it on / off.")]
        private OCSaveButtonMode _saveButtonMode = OCSaveButtonMode.Auto;
        [SerializeField, Tooltip("Open and close the panel with Toggle Key.")] private bool _hotkeyEnabled = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField, Tooltip("Open the panel by tapping one screen corner several times in quick succession.")]
        private OCCornerTap _cornerTap = new();
        [SerializeField] private OCPasscode _passcode = new();
        [SerializeField] private bool _startVisible;
        [SerializeField] private OCPanelLayout _layout = new();

        public OCPanelDefinition() { }

        internal OCPanelDefinition(string name, OCRuntimePanel prefab)
        {
            _name = name;
            _prefab = prefab;
            _hotkeyEnabled = false;
        }

        public string Name => _name;
        public bool Enabled => _enabled;
        public OCRuntimePanel? Prefab => _prefab;
        public int SortOrder => _sortOrder;

        /// <summary>Ids of the Audiences this Runtime Panel serves; empty means it shows every Exposed Entry.</summary>
        public IReadOnlyList<string> AudienceIds => _audienceIds;

        public OCSaveButtonMode SaveButtonMode => _saveButtonMode;
        public bool HotkeyEnabled => _hotkeyEnabled;
        public KeyCode ToggleKey => _toggleKey;
        public OCCornerTap CornerTap => _cornerTap;
        public OCPasscode Passcode => _passcode;
        public bool StartVisible => _startVisible;
        public OCPanelLayout Layout => _layout;
    }
}
