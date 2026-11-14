# Commands live in DSM and register themselves

ADR 0001 scoped DSM to on-site settings. The same on-site technician also needs to run actions
(clear a cache, reconnect, show a test pattern) from the same Runtime Panel, so we widen the scope
to on-site operator tools and add Commands to DSM instead of a separate package with a plug-in
seam nobody else needs yet. A Command is a `[DSMCommand]` method on an object that registers
itself with DSM while it is enabled. We rejected scanning assemblies for the attribute at startup:
it only finds static methods, because reaching instance methods would need a runtime search for
objects, and it would list Commands whose owner isn't alive.

## Consequences

- A `[DSMCommand]` method does nothing until its owner registers; forgetting the call hides the
  Command silently.
- Commands take no parameters. Input comes from Entries the operator sets in the Settings tab.
- Commands name Audiences by name, not id, because an attribute can't hold an id picked from the
  Config. Renaming an Audience breaks those references; an unknown name is reported at
  registration.
- DSM still persists nothing about Commands; the Save File stays Overrides only.
