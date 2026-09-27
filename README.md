# DataSaveManager (DSM)

A small settings store for Unity: tweakable settings of an installed app (values an on-site
technician adjusts), not player progress. Every Entry has a typed Default defined in a Config
asset; only the Overrides an operator sets are persisted, in a single hand-editable JSON Save
File.

## Install

Add via Package Manager → Add package from git URL:

```
https://github.com/draftsama/data-save-manager.git
```

Dependencies (installed automatically, see `package.json`):

- [UniTask](https://github.com/Cysharp/UniTask) — async/await and `IUniTaskAsyncEnumerable`
- [Newtonsoft Json for Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html) (`com.unity.nuget.newtonsoft-json`)

The Runtime Panel needs TextMeshPro and uGUI (`com.unity.ugui`) in the project. The Input System
package is optional — DSM falls back to the legacy Input Manager, or to no hotkey at all, if it
isn't installed.

## Quick start

1. **DSM › Create Config Asset** — creates `Assets/Resources/DSMConfig.asset`.
2. **DSM › Open Manager** — add Entries (key, type, default, label, exposed flag).
3. Read and write values from code:

```csharp
using DataSaveManager;

// Read: Override if one was set, else the Entry's Default, else default(T)
var speed = DSM.Get<float>("speed");

// Read with an explicit fallback, used only when there's neither an Override nor a Default
var gravity = DSM.Get("gravity", -9.81f);

// Write an Override
DSM.Set("speed", 7f);

// Persist Overrides to the Save File
DSM.Save();
```

Watch a value for every change (initial value, then every subsequent Set/Load/Reset):

```csharp
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using DataSaveManager;

DSM.WatchAsync<float>("speed")
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
  and persists like any Override, but DSM logs one warning the first time it's touched and never
  adds it to the Config automatically.

## API reference

All members are synchronous and must be called from the main thread.

| Member | Description |
|---|---|
| `DSM.Store` | The active `DSMStore`, created lazily from `Resources/DSMConfig.asset` on first access. |
| `DSM.Configure(DSMConfig config)` | Saves and disposes the current store (if dirty), then creates and loads a new store for `config`. |
| `DSM.Get<T>(string key)` | Returns the Value for `key`, or `default(T)` if there is neither an Override nor a usable Default. |
| `DSM.Get<T>(string key, T fallback)` | Returns the Value for `key`, or `fallback` if there is neither an Override nor a usable Default. |
| `DSM.Set<T>(string key, T value)` | Sets an Override for `key`. Triggers the AutoSave debounce if AutoSave is enabled. |
| `DSM.HasOverride(string key)` | Returns true if `key` has an explicit Override. |
| `DSM.Reset(string key)` | Removes the Override for `key`, falling back to its Default. |
| `DSM.ResetAll()` | Removes every Override, falling back to Defaults. |
| `DSM.Save()` | Synchronously writes all Overrides to the Save File. |
| `DSM.Load()` | Synchronously reads Overrides from the Save File, replacing the in-memory set. |
| `DSM.WatchAsync<T>(string key)` | Returns an `IUniTaskAsyncEnumerable<T>` that emits the current Value immediately, then again on every subsequent change. |
| `DSM.Config` | The active `DSMConfig`. |
| `DSM.SaveFilePath` | The resolved absolute path of the Save File. |

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

Set on the `DSMConfig` asset (`Assets/Resources/DSMConfig.asset`), either in the Inspector or via
**DSM › Open Manager**:

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

If the Save File is missing, DSM starts with no Overrides (every Entry reads its Default). If the
Save File exists but fails to parse, DSM logs a warning and falls back to no Overrides rather than
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

### DSM Manager window (**DSM › Open Manager**)

- **Settings** — edits AutoSave, AutoSave Debounce, Save Directory, and File Name on the Config
  asset; shows the resolved Save File path with buttons to open its folder or delete the file.
- **Entries** — one row per Entry Definition: key, type, label, exposed toggle, default value,
  current value, Reset, and remove. Duplicate keys are highlighted and reported.
- **Add Entry** — key and type for a new Entry Definition.
- **Footer** — Reset All Values, and a live count of current Overrides.

In Edit mode, changing an Entry's current value saves the Save File immediately. In Play mode,
value edits are live on the running `DSM.Store` and persisted whenever AutoSave next fires (or on
quit).

### Other menu items

- **DSM › Create Config Asset** — creates `Assets/Resources/DSMConfig.asset` if it doesn't exist
  yet, otherwise selects the existing one.
- **DSM › Open Save Folder** — reveals the resolved Save File's directory in Finder/Explorer.

## Runtime Panel

An in-game UI for an operator to view and edit Exposed Entries while the app runs.

The Color widget takes a hex code (`#RGB`, `#RRGGBB`, or `#RRGGBBAA`; the leading `#` is optional).

1. Drag `Prefab/DSMRuntimePanel.prefab` into a scene.
2. Make sure the scene has an `EventSystem` (not included in the prefab).
3. Mark the Entries you want visible as **Exposed** in the DSM Manager window.
4. Press the toggle key (default `F1`, via the Input System if installed, otherwise the legacy
   Input Manager) to show/hide the panel. It also exposes Save and Reset All buttons, and Close.

Regenerate or restyle the default widget prefabs with **DSM › Build Runtime Panel Prefabs** — this
overwrites the existing prefabs and the `DSMWidgetConfig` asset under `Prefab/`.

### Custom widgets

- Subclass `DSMWidget<T>`: implement `Show(T value)` to render, and call `Commit(value)` when the
  user edits the control.
- Or implement `IDSMWidget` directly for full control over `Setup(DSMEntryDefinition entry)`.

Either way, assign your widget's prefab to the matching type slot on a `DSMWidgetConfig` asset,
and assign that asset to the `DSMRuntimePanel`'s Widget Config field.

## Not a secure store

The Save File is plain, readable, hand-editable JSON — anyone with file access can read or change
it. Do not store secrets or player progression you need to protect from tampering; see
[ADR 0001](docs/adr/0001-scope-to-on-site-settings.md) for the reasoning behind this scope.

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

EditMode tests live in the `DSM.Tests.Editor` assembly (`Tests/Editor/`). Run them from
**Window › General › Test Runner** (EditMode tab).
