# Runtime Panel UI is built in code, not from prefabs

ADR 0005 kept the Runtime Panel and its rows as prefabs, generated from code by an Editor menu and
referenced from each Panel Definition and a Widget Config asset. We now build the whole panel in code
when a Panel Definition's Runtime Panel is created, and drop the prefabs, the Widget Config and the
Panel Definition's Prefab field. The prefabs were never edited by hand (they were regenerated from
the same code), so they only added a step to forget after every UI change and an asset to keep in
sync with the scripts.

## Consequences

- Changing the panel's look means changing `OCPanelFactory`; the **Layout** settings on a Panel
  Definition are the only per-project styling.
- A project can no longer plug in its own widget prefab for a Data Type.
- The control sprites are drawn in code (`OCUiSprites`), because Unity's built-in UI skin sprites can
  only be loaded in the Editor, so the controls look flatter than Unity's default skin.
- The UI references are assigned by field name through reflection (`OCFieldWiring`); a renamed field
  fails loudly when a panel is created, and the PlayMode tests create one.
- Creating a panel needs TextMeshPro's default font (TMP Essential Resources); without it no panel is
  created and an error is logged.
