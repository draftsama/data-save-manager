# Audiences filter Runtime Panels; apps share one Config

Some projects build several apps (e.g. Admin and Display) that each need a different set of
Exposed Entries on their Runtime Panel, with some Entries in common. We keep one Config for every
build and let each Runtime Panel pick the Audiences it serves, instead of a Config per app or
per-build `#if` defines. A Config per app duplicates the Entries the apps share, and their
Defaults drift apart; defines force a recompile to switch apps and hide the other app's Entries
from the Editor.

## Consequences

- An Audience is a Presentation Hint only: an Entry hidden from every Runtime Panel in an app
  still exists there, so code and Bindings keep working against its Default or Override.
- Each app keeps its own Save File, so a shared Entry shares its Definition, not its Value.
- An Entry naming no Audience shows on every Runtime Panel. Forgetting to tag a sensitive Entry
  exposes it everywhere, so removing an Audience that is still named is refused rather than
  silently untagging Entries.
- A Reset from a Runtime Panel covers only the Entries that Runtime Panel shows.
