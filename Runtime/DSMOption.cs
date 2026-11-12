#nullable enable

using System;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>One choice of an Int Entry shown as a dropdown: the text the operator sees and the int that is stored.</summary>
    [Serializable]
    public sealed class DSMOption
    {
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private int _value;

        public DSMOption() { }

        public DSMOption(string label, int value)
        {
            _label = label;
            _value = value;
        }

        public string Label
        {
            get => _label;
            set => _label = value;
        }

        public int Value
        {
            get => _value;
            set => _value = value;
        }
    }
}
