#nullable enable

namespace OperatorConsole
{
    /// <summary>The optional parts of an Entry Action registered from code instead of with <see cref="OCEntryActionAttribute"/>.</summary>
    public sealed class OCEntryActionOptions
    {
        public bool Confirm { get; set; }
        public int Order { get; set; }
    }
}
