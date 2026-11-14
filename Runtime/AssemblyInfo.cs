using System.Runtime.CompilerServices;

// OperatorConsole.Tests.Editor is a separate assembly, so internal test-only hooks such as
// OCStore's internal ctor and ChangedForTests event are unreachable without this grant.
[assembly: InternalsVisibleTo("OperatorConsole.Tests.Editor")]
[assembly: InternalsVisibleTo("OperatorConsole.Tests.Runtime")]

// OperatorConsole.Editor reuses internal runtime pieces (OCSerializer, OCStore.SetToken) rather than
// duplicating JSON conversion logic for the manager window.
[assembly: InternalsVisibleTo("OperatorConsole.Editor")]
