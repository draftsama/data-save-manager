# Scope DSM to on-site settings, not player progress

DSM persists tweakable settings of an installed app (values an on-site technician adjusts), not
player progress. For that scope we removed encryption and key rotation, multiple save slots, save
versioning/migration, schema enforcement, and the generated `DSMConstant` class: v2 keeps one
plain, hand-editable JSON Save File holding only Overrides, with Defaults defined in the Config
asset. These features were built in v1 but added cost (main-thread PBKDF2 hitches, a sync/async
deadlock, stale-file bugs) with no user who needed them.

## Consequences

- Save Files are readable and editable by anyone with file access — do not store secrets or
  anything a player could profit from tampering with.
- Changing an Entry's key or type has no migration path; old Overrides for a renamed key become
  ad-hoc keys and must be cleaned up by hand.
- If a future project needs player saves, build that as a separate module rather than regrowing
  these features here.
