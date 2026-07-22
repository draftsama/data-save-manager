#nullable enable
#if UNITY_EDITOR

using System.Runtime.CompilerServices;

// DMS.Tests.Editor drives the window's slot-state model (DSMManagerSlotOps) directly rather
// than through IMGUI, which needs access to these internal Editor types.
[assembly: InternalsVisibleTo("DMS.Tests.Editor")]

#endif
