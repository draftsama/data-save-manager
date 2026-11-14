#nullable enable

namespace OperatorConsole
{
    /// <summary>Optional companion to <see cref="IOCWidget"/>: a row that wants the Runtime Panel's layout settings applied to it.</summary>
    public interface IOCLayoutAware
    {
        void ApplyLayout(OCPanelLayout layout);
    }
}
