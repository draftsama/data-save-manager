# Runtime Panel is drawn with UI Toolkit

Supersedes [ADR 0007](0007-runtime-panel-ui-built-in-code.md). The Runtime Panel is still built in
code from its Panel Definition, but with UI Toolkit instead of uGUI and TextMeshPro. Each panel gets a
`UIDocument` and a `PanelSettings` created at runtime (1920×1080 reference, match 0.5, the Panel
Definition's Sort Order). Its look comes from one USS file loaded through a theme in `Resources`.

We moved for a flatter, simpler build: flexbox and USS replace the layout groups, the
`ContentSizeFitter`s and the sprites drawn in code, and the panel no longer needs TextMeshPro's
default font.

## Consequences

- The package ships two text assets in `Runtime/Resources/OperatorConsole`: `OCRuntimeTheme.tss`,
  which imports Unity's default runtime theme and `OCRuntimePanel.uss`. A panel isn't created
  without the theme. Unity can't parse USS at runtime, so the look can't live in C#.
- Changing the panel's look means changing that USS. The Layout settings are applied as inline
  styles, because C# can't set USS custom properties.
- `OCPanelFactory`, `OCFieldWiring`, `OCUiSprites`, `OCCircleSprites`, `OCWidgetTemplates`,
  `OCPanelTab` and `IOCWidget` are gone. Dropping `OCFieldWiring` also drops its reflection wiring by
  field name.
- The widgets, rows and Lock Screen are internal `VisualElement`s instead of public
  `MonoBehaviour`s. This is a breaking change (6.0.0).
- A project whose own UI is uGUI now runs two UI stacks. The Runtime Panel draws above the app's
  Screen Space Overlay canvases and takes input through the same `EventSystem`.
- Text uses the default runtime theme's font. Scripts that font lacks (Thai, for example) need a font
  asset added to the theme.
