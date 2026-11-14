# Runtime Panels are created from Panel Definitions in the Config

Each Runtime Panel used to be a prefab instance placed in a scene, with its Audiences, Open
Triggers, Panel Lock and layout set on the component. We now describe each Runtime Panel as a
Panel Definition in the Config, edited in the Manager Window, and the app creates one Runtime
Panel per enabled Panel Definition when it starts and keeps it across scene loads. We rejected
building the whole panel in code (it would drop restyling through prefabs and the Widget Config)
and supporting scene-placed panels alongside Panel Definitions (settings would live in two places,
and one Open Trigger could open two panels).

## Consequences

- Panel settings are edited in one window next to the Entries and Audiences they refer to, so
  refusing to remove an Audience a Runtime Panel still names is checked in one place.
- Edits to a Panel Definition in Play mode are kept, because the Config is an asset; edits to a
  scene component used to revert.
- Code no longer holds an Inspector reference to a Runtime Panel; it finds one by its Panel
  Definition's name, so renaming a Panel Definition breaks code that used the old name.
- A Runtime Panel brings its own Canvas, and an EventSystem when the scene has none.
- Scenes that already hold a Runtime Panel are migrated once into a Panel Definition, then the
  scene instance is removed.
