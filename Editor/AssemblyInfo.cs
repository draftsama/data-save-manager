#nullable enable
#if UNITY_EDITOR

using System.Runtime.CompilerServices;

// Grants DSM.Tests.Editor access to internal Editor types, for future Editor-window tests
// (phase 2 rebuilds the Editor window that DSM.Tests.Editor exercised in v1).
[assembly: InternalsVisibleTo("DSM.Tests.Editor")]

#endif
