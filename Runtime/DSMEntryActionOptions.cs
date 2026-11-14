#nullable enable

namespace DataSaveManager
{
    /// <summary>The optional parts of an Entry Action registered from code instead of with <see cref="DSMEntryActionAttribute"/>.</summary>
    public sealed class DSMEntryActionOptions
    {
        public bool Confirm { get; set; }
        public int Order { get; set; }
    }
}
