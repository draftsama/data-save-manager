#nullable enable

namespace OperatorConsole
{
    public interface IOCWidget
    {
        /// <summary>Sets the row up against the global store. Kept for widgets written before 2.2.</summary>
        void Setup(OCEntryDefinition entry);

        /// <summary>Sets the row up to read, watch and commit its Entry through <paramref name="store"/>. Defaults to <see cref="Setup(OCEntryDefinition)"/>, which uses the global store.</summary>
        void Setup(OCEntryDefinition entry, OCStore store) => Setup(entry);
    }
}
