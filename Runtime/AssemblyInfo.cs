using System.Runtime.CompilerServices;

// DSM.Tests.Editor is a separate assembly, so internal test-only hooks such as
// DSMStore's internal ctor and ChangedForTests event are unreachable without this grant.
[assembly: InternalsVisibleTo("DSM.Tests.Editor")]
