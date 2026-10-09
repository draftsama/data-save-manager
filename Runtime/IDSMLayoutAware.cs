#nullable enable

namespace DataSaveManager
{
    /// <summary>Optional companion to <see cref="IDSMWidget"/>: a row that wants the Runtime Panel's layout settings applied to it.</summary>
    public interface IDSMLayoutAware
    {
        void ApplyLayout(DSMPanelLayout layout);
    }
}
