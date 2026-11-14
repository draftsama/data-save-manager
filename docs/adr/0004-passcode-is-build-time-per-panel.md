# Passcode is set at build time, per Runtime Panel

A Panel Lock's Passcode lives on the Runtime Panel's Panel Definition (see ADR 0005; it was on the
Runtime Panel component before), stored as a salted hash, and is set in the Manager Window when the
app is built. We rejected keeping it as an Entry (ADR 0001 keeps the
Save File plain and hand-editable, so anyone with file access could read or clear it) and a single
Passcode in the Config (it would force every Runtime Panel to share one lock, while the point of
several Runtime Panels is that a locked Admin panel can sit beside an open Technician panel).

## Consequences

- On-site operators cannot change a Passcode; changing it means a new build.
- The Passcode is a deterrent against casual visitors, not real security: a short numeric code
  has few combinations, and its hash ships inside the build.
