#nullable enable

using System;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>A named kind of operator an Exposed Entry is meant for. Entries and Runtime Panels refer to it by <see cref="Id"/>, so renaming changes nothing else.</summary>
    [Serializable]
    public sealed class DSMAudience
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _name = string.Empty;

        public DSMAudience(string id, string name)
        {
            _id = id;
            _name = name;
        }

        public string Id => _id;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public static string NewId() => Guid.NewGuid().ToString("N");
    }
}
