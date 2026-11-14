#nullable enable

namespace OperatorConsole
{
    /// <summary>A Runtime Panel element that takes the Panel Definition's layout settings.</summary>
    internal interface IOCLayoutAware
    {
        void ApplyLayout(OCPanelLayout layout);
    }
}
