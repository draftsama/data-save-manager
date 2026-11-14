#nullable enable
#if UNITY_EDITOR

using System.Runtime.CompilerServices;

// Grants OperatorConsole.Tests.Editor access to internal Editor types, for future Editor-window tests
// (phase 2 rebuilds the Editor window that OperatorConsole.Tests.Editor exercised in v1).
[assembly: InternalsVisibleTo("OperatorConsole.Tests.Editor")]

#endif
