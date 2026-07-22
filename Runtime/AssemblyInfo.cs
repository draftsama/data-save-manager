using System.Runtime.CompilerServices;

// DMS.Tests.Editor is a separate assembly, so deterministic test-only hooks such as
// DSMSlot.FlushWatchers() are unreachable without this grant.
[assembly: InternalsVisibleTo("DMS.Tests.Editor")]
