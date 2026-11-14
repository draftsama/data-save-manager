# One package for Operator tools, named OperatorConsole

The package started as a settings store (ADR 0001) and has grown a Runtime Panel, Commands, Entry
Actions and Panel Locks; a log console, a profiler and live stats are planned next. We keep all of
it in one package and rename it from DataSaveManager to OperatorConsole, because every app that
uses the store also uses the Runtime Panel, and the name should describe the whole. We rejected
splitting the settings store from the Operator tools (two packages that always ship together) and
the name AdminConsole (Admin is already an Audience, and the tools serve every Audience).

ADR 0001 still holds for the settings store: one plain, hand-editable Save File holding only
Overrides, and no player progress. It no longer limits the package to settings.

## Consequences

- The repository, package id, namespace and assembly names change in a major version; GitHub
  redirects the old repository URL, but consumers should update their submodule or Package
  Manager URL.
- Data already on disk and in assets keeps working: the default Save File location
  (`persistentDataPath/DSM/save.json`) and the `DSMConfig` Resources name stay, and serialized
  types moved to the new namespace keep their old identity for Unity.
- New tools (logs, profiler, stats) belong here without another rename.
