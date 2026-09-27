using System.Runtime.CompilerServices;

// DSM.Tests.Editor is a separate assembly, so internal test-only hooks such as
// DSMStore's internal ctor and ChangedForTests event are unreachable without this grant.
[assembly: InternalsVisibleTo("DSM.Tests.Editor")]

// DSM.Editor reuses internal runtime pieces (DSMSerializer, DSMStore.SetToken) rather than
// duplicating JSON conversion logic for the manager window.
[assembly: InternalsVisibleTo("DSM.Editor")]
