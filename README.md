# OperatorConsole (OC)

A small settings store for Unity: tweakable settings of an installed app (values an on-site
technician adjusts), not player progress. Every Entry has a typed Default defined in a Config
asset; only the Overrides an operator sets are persisted, in a single hand-editable JSON Save
File.

## Install

Add via Package Manager → Add package from git URL:

```
https://github.com/draftsama/operator-console.git
```

Dependencies (installed automatically, see `package.json`):

- [UniTask](https://github.com/Cysharp/UniTask) — async/await and `IUniTaskAsyncEnumerable`
- [Newtonsoft Json for Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html) (`com.unity.nuget.newtonsoft-json`)

The Runtime Panel needs TextMeshPro and uGUI (`com.unity.ugui`) in the project. The Input System
package is optional — OC falls back to the legacy Input Manager, or to no hotkey at all, if it
isn't installed.

## Quick start

1. **Draft › Operator Console › Open Manager** — click **Create Config Asset** (creates `Assets/Resources/DSMConfig.asset`), then add Entries (key, type, default, label, exposed flag).
2. Read and write values from code:

```csharp
using OperatorConsole;

// Read: Override if one was set, else the Entry's Default, else default(T)
var speed = OC.Get<float>("speed");

// Read with an explicit fallback, used only when there's neither an Override nor a Default
var gravity = OC.Get("gravity", -9.81f);

// Write an Override
OC.Set("speed", 7f);

// Persist Overrides to the Save File
OC.Save();
```

Watch a value for every change (initial value, then every subsequent Set/Load/Reset):

```csharp
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using OperatorConsole;

OC.WatchAsync<float>("speed")
    .ForEachAsync(v => moveSpeed = v, destroyCancellationToken)
    .Forget();
```

## Concepts

- **Value** — the current value of an Entry: its **Override** if one exists, otherwise its
  **Default**, otherwise `default(T)`.
- **Override** — a Value explicitly set for an Entry. Only Overrides are persisted; an Entry
  that was never set keeps following its Default, even if the Default later changes.
- `Get(key, fallback)` — resolution is still Override → Default; `fallback` is only used when
  there is neither an Override nor a Default (i.e. no Entry Definition, or the Entry Definition's
  default JSON is empty/unparsable).
- **Ad-hoc key** — a key used in code with no matching Entry Definition in the Config. It works
  and persists like any Override, but OC logs one warning the first time it's touched and never
  adds it to the Config automatically.

## API reference

All members are synchronous and must be called from the main thread.

| Member | Description |
|---|---|
| `OC.Store` | The active `OCStore`, created lazily from `Resources/DSMConfig.asset` on first access. |
| `OC.Configure(OCConfig config)` | Saves and disposes the current store (if dirty), then creates and loads a new store for `config`. Runtime Panels already created are destroyed and created again from `config`. |
| `OC.Get<T>(string key)` | Returns the Value for `key`, or `default(T)` if there is neither an Override nor a usable Default. |
| `OC.Get<T>(string key, T fallback)` | Returns the Value for `key`, or `fallback` if there is neither an Override nor a usable Default. |
| `OC.Set<T>(string key, T value)` | Sets an Override for `key`. Triggers the AutoSave debounce if AutoSave is enabled. |
| `OC.HasOverride(string key)` | Returns true if `key` has an explicit Override. |
| `OC.Reset(string key)` | Removes the Override for `key`, falling back to its Default. |
| `OC.ResetAll()` | Removes every Override, falling back to Defaults. |
| `OC.ResetExposedEntries()` | Removes the Overrides of Exposed Entries only, as the Runtime Panel's Reset does. |
| `OC.Save()` | Synchronously writes all Overrides to the Save File. |
| `OC.Load()` | Synchronously reads Overrides from the Save File, replacing the in-memory set. |
| `OC.WatchAsync<T>(string key)` | Returns an `IUniTaskAsyncEnumerable<T>` that emits the current Value immediately, then again on every subsequent change. |
| `OC.Config` | The active `OCConfig`. |
| `OC.SaveFilePath` | The resolved absolute path of the Save File. |

## Supported types

| Type | JSON shape |
|---|---|
| Int | `0` |
| Float | `0.0` |
| Bool | `false` |
| String | `""` |
| Vector2 | `{"x":0.0,"y":0.0}` |
| Vector3 | `{"x":0.0,"y":0.0,"z":0.0}` |
| Color | `{"r":1.0,"g":1.0,"b":1.0,"a":1.0}` |

## Config settings

Set on the `OCConfig` asset (`Assets/Resources/DSMConfig.asset`), either in the Inspector or via
**Draft › Operator Console › Open Manager**:

| Field | Default | Description |
|---|---|---|
| Auto Save | `true` | Automatically saves shortly after any Override changes. |
| Auto Save Debounce | `1` second | Delay after the last change before AutoSave writes the Save File. |
| Save Directory | empty | See path resolution below. |
| File Name | `save.json` | Name of the Save File. |

Saving also happens once on `Application.quitting` if there are unsaved changes, regardless of
AutoSave.

### Save path resolution

- **Save Directory empty** → `Application.persistentDataPath/DSM/`.
- **Save Directory relative** → resolved from the folder containing `Application.dataPath` (next
  to the executable in a build, the project root in the Editor).
- **Save Directory absolute** → used as-is.

The full Save File path is `<resolved directory>/<File Name>`.

If the Save File is missing, OC starts with no Overrides (every Entry reads its Default). If the
Save File exists but fails to parse, OC logs a warning and falls back to no Overrides rather than
throwing.

Example Save File — a flat JSON object of only the Overrides that have been set:

```json
{
  "speed": 7.0,
  "playerName": "Alice",
  "spawnPoint": { "x": 0.0, "y": 1.0, "z": 0.0 }
}
```

## Editor

### Operator Console window (**Draft › Operator Console › Open Manager**)

A UI Toolkit window with a sidebar for its four sections. It follows the Editor's dark or light skin.

- **Entries** — one row per Entry Definition: type, key and label, default value, current value
  (a dot marks an Override), and Reset. Click a row's key to show its settings: key, type, label,
  Exposed, Slider (Int/Float), Dropdown Options (Int), Header and Audience, plus Remove Entry.
  Duplicate keys are highlighted and reported. Drag a row by its handle to reorder it — that order
  is the Runtime Panel order (reordering is off while searching). **Search** filters by key or
  label; **View as** dims the Entries that Audience's Runtime Panel would not show.
- **Add Entry** (bottom bar) — key and type for a new Entry Definition, next to Reset All Values
  and a live count of current Overrides.
- **Runtime Panels** — one card per Panel Definition (see below).
- **Commands** — in Play mode, every Registered Command with its status and a Run button (it asks first
  when the Command has `Confirm`). **View as** dims the Commands that Audience's Runtime Panel would not show.
- **Settings** — edits AutoSave, AutoSave Debounce, Save Directory, and File Name on the Config
  asset; shows the resolved Save File path with buttons to open its folder or delete the file; and
  lists the Config's Audiences.

In Edit mode, changing an Entry's current value saves the Save File immediately. In Play mode,
value edits are live on the running `OC.Store` and persisted whenever AutoSave next fires (or on
quit).

## Runtime Panel

An in-game UI for an operator to view and edit Exposed Entries while the app runs.

The Color widget takes a hex code (`#RGB`, `#RRGGBB`, or `#RRGGBBAA`; the leading `#` is optional).

1. In the Operator Console window, open **Runtime Panels** and press **+ Add Panel**. Each enabled
   **Panel Definition** becomes one Runtime Panel when the app starts; nothing goes in a scene.
2. Mark the Entries you want visible as **Exposed**.
3. Press the toggle key (default `F1`, via the Input System if installed, otherwise the legacy
   Input Manager) to show/hide the panel. The footer has Save, Reset to Defaults, and Close buttons.

A Runtime Panel is created after the first scene loads and lives until the app quits, across scene
loads. It brings its own Canvas (**Sort Order**, default 1000) and adds an `EventSystem` to any scene
that has none. Several Panel Definitions give several panels, e.g. an open Technician panel on `F1`
beside a locked Admin panel on a Corner Tap; two that share an Open Trigger both open on it, but only one Lock Screen shows at a time: of
the locked ones, only the first in the Manager's list opens (the Manager warns). **Enabled** off keeps a Panel Definition, Passcode included, without creating its panel.
The panel's UI is built in code when the panel is created, so there is no prefab to assign or
rebuild. See [ADR 0005](docs/adr/0005-runtime-panels-from-panel-definitions.md) and
[ADR 0007](docs/adr/0007-runtime-panel-ui-built-in-code.md).

Code finds a panel by its Panel Definition's **Name**: `OC.Panels.Get("Admin")` (null and one
warning when no panel has that name), `OC.Panels.TryGet`, `OC.Panels.All`, `OC.Panels.HideAll()`.
Renaming a Panel Definition breaks code that used the old name. `OCRuntimePanel.IsOpen` is true while
any panel or its Lock Screen is open.

Panel Definition edits made in the Manager during Play mode apply at once and, because the Config is
an asset, are kept after Play mode ends. Adding or removing a Panel Definition takes effect the next time
Play mode starts.

**Moving from a scene-placed panel:** a `OCRuntimePanel` placed in a scene now stays hidden and logs
a warning. Open its scene, press **Import Scene Panels** under Runtime Panels to turn each one into a
Panel Definition (Audiences, Open Triggers, Passcode, Layout and sort order carry over), then remove
the panel from the scene and save it. Replace Inspector references to the panel with `OC.Panels`.

- **Reset to Defaults** asks for confirmation in a popup, then resets only the Exposed Entries.
  Overrides of non-exposed Entries and of ad-hoc keys (keys used in code without an Entry Definition)
  are left alone. Code gets the same Reset from `OC.ResetExposedEntries()`; `OC.ResetAll()` still clears every Override.
- **Save Button Mode** (on the Panel Definition) controls the Save button: `Auto` shows it only
  when AutoSave is off, `Always` always shows it, `Never` hides it.
- **Close** (and the toggle key) cancels an open reset confirmation. With AutoSave on, it also saves any
  pending changes immediately instead of waiting for the debounce; with AutoSave off it does not save.

### Open Triggers and Panel Lock

An operator opens the panel by hand with its **Open Triggers**, set on its Panel Definition:

- **Hotkey Enabled** + **Toggle Key** — the key opens and closes the panel.
- **Corner Tap** — tapping (or clicking) one screen **Corner** **Tap Count** times, each within
  **Max Interval** seconds of the last, opens the panel. The zone is a square whose side is **Size
  Percent** of the screen's shorter side. It only opens; close the panel with Close or the hotkey.
  Taps on the app's own UI in that corner still reach it.

Both can be on at once. Under **Panel Lock**, type 4–8 digits into **New Passcode** and press **Set
Passcode** to lock the panel: every Open Trigger then shows the full-screen **Lock Screen** keypad first.
The panel opens as soon as the right digits are entered; a wrong code shakes, shows "Wrong passcode",
and can be retried. Cancel, Esc, or the hotkey closes the Lock Screen. The keyboard's digit keys and
Backspace work too. Only a salted hash of the Passcode is stored, in the Config, so changing it
needs a new build. See [ADR 0004](docs/adr/0004-passcode-is-build-time-per-panel.md).

Code chooses whether to ask: `RequestOpen()` and `Toggle()` go through the Lock Screen, `Show()`
opens at once. `IsOpen` is true while a Lock Screen is up.

### Layout

Each Panel Definition has a **Layout** group. Changes made in the Manager during Play mode apply to the
open panel immediately.

- **Label Width**, **Row Height**, **Row Spacing**, **Font Size** — row metrics; the font size applies to
  every text in a row.
- **Size Mode** — `Fixed` uses Panel Size, Anchor and Offset; `Full Screen` stretches the panel over the
  whole screen, inset by **Full Screen Padding**.
- **Panel Size**, **Anchor** (Center, Left, Right, Top, Bottom, or a corner), **Offset** — where the panel
  sits on screen in `Fixed` mode. Anchor sets both the anchor and the pivot; Offset is relative to it.
- **Background Color** — the panel's background.
- **Label Overflow** — `Ellipsis` keeps each label on one line at Row Height; `Wrap` lets a row grow
  taller than Row Height to fit its wrapped label (Row Height becomes the minimum).
- **Float Decimals** (0-6) — decimals shown by float sliders and used to round slider commits.
- **Scrollbar Width**, **Scroll Sensitivity** — width of the vertical scrollbar and mouse-wheel speed.
  The scrollbar hides itself when all rows fit; the list scrolls with the wheel or by dragging.
- **Header Font Size**, **Header Spacing** — font size of Entry headers and the extra space above each one.
- **Tab Height**, **Command Description Font Size**, **Command Button Width** — the Settings / Commands
  tabs, and the Description, error line and Run button of each Command row.
- **Lock Key Size**, **Lock Key Spacing**, **Lock Key Font Size**, **Lock Dot Size**, **Lock Background
  Alpha** — the Lock Screen keypad, its passcode dots, and how much of the app shows through behind it.
- **Entry Action Button Width**, **Entry Action Error Font Size** — each Entry Action button beside a
  widget (longer Titles end in an ellipsis) and the error line under a row whose Entry Action failed.

### Headers

In the Operator Console, an Exposed Entry of any type gets a **Header** line: a toggle plus a text field.
When the toggle is on and the text is not blank, the Runtime Panel shows the text as a title row
directly above that Entry's row, to start a visual group. Turning the toggle off keeps the text. A
header is presentation only and has no effect on the Value.

### Range sliders

In the Operator Console, an Exposed Entry of type Int or Float gets a **Range** line: a toggle plus Min and
Max. With a Range the Runtime Panel shows the Entry as `Label | Slider | InputField`; without one it
shows the plain input. Min must be less than Max — otherwise the fields are tinted red and the panel
logs a warning and falls back to the plain input.

A Range is a presentation hint only and never clamps the Value: the slider handle sits at the clamped
position, while the input box shows the real Value and accepts anything typed. Int sliders step in whole
numbers; float sliders round committed values to **Float Decimals**.

### Widgets

The panel builds one row per Exposed Entry from its built-in widgets: one per Data Type, plus Int and
Float sliders for Entries with a valid Range and a dropdown for Entries with Options. They derive from
`OCWidget<T>` (`Show(T value)` renders, `Commit(value)` writes) and implement `IOCLayoutAware` to follow
the Layout settings. Since the UI is built in code, a project can no longer swap in its own widget
prefab; restyle through **Layout**, or change `OCPanelFactory` in this package.

### Commands

A Command is an action an operator runs from the panel's **Commands** tab, such as reconnecting a
device or clearing a cache. Mark a method with `[OCCommand]` and have its owner register while it is
enabled:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using OperatorConsole;
using UnityEngine;

public sealed class Projector : MonoBehaviour
{
    void OnEnable()  => OC.Commands.Register(this);
    void OnDisable() => OC.Commands.Unregister(this);

    [OCCommand("Reconnect", Description = "Drops the link and opens it again.", Header = "Projectors")]
    private async UniTask Reconnect(CancellationToken ct) { /* ... */ }

    [OCCommand("Show test pattern", Confirm = true, Audiences = new[] { "Technician" }, Order = 10)]
    private void ShowTestPattern() { /* ... */ }
}
```

- **Signature** — no parameters, or a single `CancellationToken`; returns `void` or `UniTask`. Any
  access level. Other methods are skipped with an error at `Register`, and the Editor warns about them
  after each script reload.
- **Static methods** — `OC.Commands.Register(typeof(Maintenance))` / `Unregister(typeof(Maintenance))`.
  `Register(this)` reads instance methods only.
- **From code** — `OC.Commands.Register(owner, "Title", ct => ..., new OCCommandOptions { ... })`;
  `Unregister(owner)` removes it too.
- **Running** — a dot beside the Run button turns amber while it runs (the button reads `Running…` and is
  disabled), then green, or red with the exception message under the Description; the exception is also logged. The outcome stays with the Command, so it is still
  there when the panel reopens. A Command keeps running when the panel closes. `Unregister` cancels the
  token; a cancelled run isn't a failure.
- **Header** — starts a visual group; consecutive Commands with the same Header show it once.
- **Same Title twice** — two owners that register the same Title (two projectors) show as
  `Reconnect (Projector L)` and `Reconnect (Projector R)`, from the owners' GameObject names.
- **Audiences** — named by **name** (not id) because an attribute can't hold an id. Renaming an Audience
  in the Config drops it from Commands that named the old name; an unknown name is warned about at
  `Register` and matches no Runtime Panel. Same rule as Entries otherwise.
- **Confirm** — reuses the panel's confirm popup (`Run "<Title>"?`).
- **Tabs** — the tab bar appears only when the panel has a Command to show, so a project without
  Commands sees no change. The panel always opens on Settings, or on Commands when it shows no Exposed
  Entry.

`[OCCommand]` derives from Unity's `PreserveAttribute`, so IL2CPP code stripping keeps the methods.

### Entry Actions

An Entry Action is a button beside one Exposed Entry whose result becomes that Entry's Value — browse
for a folder, fetch a token. It registers with the same `OC.Commands.Register(this)` call as Commands:

```csharp
public sealed class ContentFolders : MonoBehaviour
{
    void OnEnable()  => OC.Commands.Register(this);
    void OnDisable() => OC.Commands.Unregister(this);

    [OCEntryAction("contentFolderPath", "Browse…")]
    UniTask<string> Browse(string current, CancellationToken ct) => MyFileDialog.PickFolder(current, ct);

    [OCEntryAction("apiToken", "Fetch", Confirm = true)]
    async UniTask<string> Fetch(CancellationToken ct)
    {
        using var request = UnityWebRequest.Get(OC.Get<string>("apiUrl") + "/token");
        await request.SendWebRequest().WithCancellation(ct);
        if (request.result != UnityWebRequest.Result.Success)
            throw new InvalidOperationException(request.error);
        return request.downloadHandler.text.Trim();
    }
}
```

- **Signature** — takes nothing, a `CancellationToken`, the current Value, or the current Value then a
  token; returns the new Value as `T` or `UniTask<T>`. `T` must match the Entry's Data Type (int, float,
  bool, string, Vector2, Vector3, Color), or `Register` logs an error and skips it.
- **Leave the Value alone** — throw `OperationCanceledException` (e.g. the operator closed the dialog).
- **Result** — set exactly as if the operator typed it: an Override at once, saved by the usual rules.
  It is set even if the Value changed while the action ran (last write wins).
- **Where it shows** — wherever its Entry shows; it has no Audiences or Header of its own. An unknown
  Key or a non-Exposed Entry is warned about at `Register`.
- **While running** — the whole row is locked (input and its other actions) and the button reads
  `Running…`. A failure shows its message on a line under the row.
- **From code** — `OC.Commands.Register<string>(owner, "contentFolderPath", "Browse…", (current, ct) => ..., new OCEntryActionOptions { Confirm = true, Order = 1 })`.
- **Order**, **Confirm**, **Same Title twice** — as for Commands.

## Binding

A no-code, one-way link from an Entry's Value to one or more members of components on the same GameObject —
no widget or hand-written code needed.

1. Add an `OC Binding` component to the GameObject.
2. Pick **Key** (an Entry from the Config), then add a **Link** per member to drive. Each Link has a
   **Component** (any component on the same GameObject), a **Member** (a public property, field, or
   single-argument method on that component whose type exactly matches the Entry's type), and its own **Format**
   for string members.
3. Every member updates live whenever the Entry's Value changes, and once immediately when the
   binding is enabled.

Binding is one-way (OC → component) and requires an exact type match — no implicit conversions — except for formatted `string` members, below.

A `string` member (for example `TMP_Text.text`) can be bound to an Entry of any type: pick a member listed as
`OC_Set…` (e.g. `OC_SetText (format → text)`), then set **Format**, a .NET composite format where `{0}` is the value (invariant culture; empty
means `{0}`). The Inspector previews the result with the current Value. Examples:

- `Speed: {0:0.0} m/s` for a Float
- `{0:N0} pts` for an Int
- `{0:F2}` for a Vector3, giving `(1.00, 2.00, 3.00)`

An invalid format logs one error at runtime and falls back to the plain value.

IL2CPP builds strip members referenced only by name through reflection, so OC generates a `link.xml` at
build time that preserves every bound member found in the built scenes and in the project's prefabs. A
binding created from code, or on a prefab outside `Assets/`, isn't covered — add `[Preserve]` to that
member yourself.

## Not a secure store

The Save File is plain, readable, hand-editable JSON — anyone with file access can read or change
it. Do not store secrets or player progression you need to protect from tampering; see
[ADR 0001](docs/adr/0001-scope-to-on-site-settings.md) for the reasoning behind this scope.

## Migrating from DataSaveManager 3.x

4.0 renames the package; nothing about the Save File or the Config changes.

- Package id `com.draftsama.datasavemanager` → `com.draftsama.operatorconsole`; the repository is
  now `operator-console`.
- Namespace `DataSaveManager` → `OperatorConsole`, assemblies `DSM.*` → `OperatorConsole.*`.
  An asmdef that references the package by name (`"DSM.Runtime"`) needs the new name; one that
  references it by GUID keeps working.
- Every `DSM*` type is now `OC*` (`DSMConfig` → `OCConfig`, `[DSMCommand]` → `[OCCommand]`,
  `IDSMWidget` → `IOCWidget`). Scenes, prefabs and assets keep their references: the scripts kept
  their GUIDs.
- The facade `DSM` is now `OC`. `DSM` still compiles, marked `[Obsolete]`, and is removed in 5.0.
- Menus moved from **Draft › DSM** to **Draft › Operator Console**; the DSM Manager window is now
  the Operator Console window.
- Unchanged: the Config is still loaded from `Resources/DSMConfig.asset`, and the default Save File
  is still `persistentDataPath/DSM/save.json`.

## Migrating from 1.x

v2 is a from-scratch rewrite around a single settings store. Removed, with no migration path:

- Save slots (`DSM.UseSlot`, `GetSlot`, `GetAllSlots`, `DeleteSlot`) — there is one Save File.
- AES encryption and the encryption key API.
- Save versioning/migration and the envelope/schema format.
- The `DSMConstant` code generator — Entry keys are plain strings, defaults live in the Config.
- `Double`, `Long`, `Vector4` data types.
- Transform/RectTransform snapshot helpers (`DSMTransformData`, `DSMRectData`).
- `SaveAsync` / `LoadAsync` — the API is synchronous now.

Old save files (slots, `.enc`, `DSMConstant`-based) are not read by v2. Existing Overrides for a
renamed or removed key become ad-hoc keys and must be cleaned up by hand.

## Tests

EditMode tests live in the `OperatorConsole.Tests.Editor` assembly (`Tests/Editor/`). Run them from
**Window › General › Test Runner** (EditMode tab).
