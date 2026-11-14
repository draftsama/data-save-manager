#nullable enable

using System;
using UnityEngine.Scripting;

namespace OperatorConsole
{
    /// <summary>
    /// Marks a method as an Entry Action: a button beside the Exposed Entry named by <see cref="Key"/>, shown once its owner calls
    /// <see cref="OCCommandRegistry.Register(object)"/>. Its result becomes that Entry's Value.
    /// The method takes nothing, a <see cref="System.Threading.CancellationToken"/>, the current Value, or the current Value then a token,
    /// and returns the new Value as T or UniTask&lt;T&gt;, T matching the Entry's Data Type. Throw <see cref="OperationCanceledException"/> to leave the Value unchanged.
    /// </summary>
    /// <remarks>Derives from <see cref="PreserveAttribute"/> because the method is reached only through reflection, which IL2CPP code stripping can't see.</remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class OCEntryActionAttribute : PreserveAttribute
    {
        public OCEntryActionAttribute(string key, string title)
        {
            Key = key;
            Title = title;
        }

        public string Key { get; }
        public string Title { get; }
        public bool Confirm { get; set; }
        public int Order { get; set; }
    }
}
