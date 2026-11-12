#nullable enable

namespace DataSaveManager
{
    public interface IDSMWidget
    {
        /// <summary>Sets the row up against the global store. Kept for widgets written before 2.2.</summary>
        void Setup(DSMEntryDefinition entry);

        /// <summary>Sets the row up to read, watch and commit its Entry through <paramref name="store"/>. Defaults to <see cref="Setup(DSMEntryDefinition)"/>, which uses the global store.</summary>
        void Setup(DSMEntryDefinition entry, DSMStore store) => Setup(entry);
    }
}
