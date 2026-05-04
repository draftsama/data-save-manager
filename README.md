# DataSaveManager (DSM)

A lightweight, slot-based save system for Unity with JSON serialization, optional AES encryption, reactive change watching, and an Editor Manager window for defining typed data constants.

---

## Features

- **Simple static API** — `DSM.Set` / `DSM.Get` from anywhere
- **Multi-slot saves** — independent named save slots per player/profile
- **Shared defaults** — all slots use `DSMConstant` as default values; new slots are seeded automatically
- **Optional AES encryption** — `.enc` files with PBKDF2 key derivation
- **Async I/O** — `SaveAsync` / `LoadAsync` via UniTask
- **Reactive watching** — `WatchAsync<T>` streams value changes as `IUniTaskAsyncEnumerable`
- **Unity type support** — Vector2/3/4, Quaternion, Color, Color32
- **Transform snapshot helpers** — `DSMTransformData`, `DSMRectData`
- **Editor Manager window** — define typed entries, manage slots, edit values, generate `DSMConstant.cs` on demand
- **Runtime Config Canvas** — expose selected entries to an in-game UI canvas; auto-generates typed input widgets per value

---

## Quick Start

### 1. Create a Config

In the Unity menu bar: **DSM › Create Config Asset**

This creates `Assets/Resources/DSMConfig.asset`. Adjust settings in the Inspector.

| Field | Description |
|-------|-------------|
| Auto Save | Save automatically after every `Set()` |
| Auto Save Debounce | Delay (seconds) before auto-save fires |
| Encrypt | Store save files as encrypted `.enc` |
| Encryption Key | Password used for AES encryption |
| Save Path | Override default `persistentDataPath/DSM` |
| Default Slot | Name of the default save slot |
| Pretty Print | Indent JSON for readability |

### 2. Use the API

```csharp
// Write
DSM.Set("playerName", "Alice");
DSM.Set("hp", 100);
DSM.Set("spawnPos", new Vector3(0, 1, 0));

// Read
var name = DSM.Get("playerName", "Hero");   // returns "Hero" if key missing
var hp   = DSM.Get("hp", 100);

// Check / Delete
if (DSM.Has("playerName")) { }
DSM.Delete("playerName");
DSM.Clear(); // remove all keys in active slot

// Save / Load
DSM.Save();
DSM.Load();
await DSM.SaveAsync();
await DSM.LoadAsync();
```

### 3. Use typed constants (recommended)

Open **DSM › Open Manager**, create entries, and click **Save DSMConstant.cs**.

```csharp
// Generated: DSMConstant.cs
public static partial class DSMConstant
{
    public const int    hp       = 100;
    public const float  speed    = 3.5f;
    public const string saveName = "Hero";
}

// Usage — no magic strings
var hp    = DSM.Get<int>(nameof(DSMConstant.hp), DSMConstant.hp);
var speed = DSM.Get<float>(nameof(DSMConstant.speed), DSMConstant.speed);
```

---

## Multi-Slot Saves

```csharp
// Switch to a named slot (loads it automatically)
DSM.UseSlot("slot2");

// Read another slot without switching
var slot = DSM.GetSlot("slot1");

// List all existing slots
string[] slots = DSM.GetAllSlots();

// Delete a slot
DSM.DeleteSlot("slot2");
```

Save files are stored as:
```
{persistentDataPath}/DSM/{slotName}.json   (unencrypted)
{persistentDataPath}/DSM/{slotName}.enc    (encrypted)
```

### Shared defaults across slots

All slots share `DSMConstant` as their default source. When a slot has no save file yet (first load), it is automatically seeded with every key and its default value from `DSMConstant`. Each slot's JSON is independent after that — changing a value in one slot never affects another.

```csharp
// Slot "001" — first load, no file on disk yet
DSM.UseSlot("001");
DSM.Get<int>("hp", 0);      // returns 100 (from DSMConstant.hp)
DSM.Get<float>("speed", 0); // returns 3.5 (from DSMConstant.speed)

// After Set, this slot has its own value
DSM.Set("hp", 75);

// Slot "002" still reads from defaults
DSM.UseSlot("002");
DSM.Get<int>("hp", 0);      // returns 100 again
```

---

## Reactive Watching

`WatchAsync<T>` emits the current value immediately, then emits again on every `Set()` for that key.

```csharp
private CancellationTokenSource _cts = new();

async void Start()
{
    await foreach (var hp in DSM.WatchAsync<int>("hp").WithCancellation(_cts.Token))
    {
        hpBar.value = hp;
    }
}

void OnDestroy() => _cts.Cancel();
```

---

## Transform Helpers

```csharp
// Capture
var snap = transform.Capture();         // DSMTransformData
DSM.Set("playerTransform", snap);

// Restore
var saved = DSM.Get("playerTransform", default(DSMTransformData));
saved.Restore(transform);

// RectTransform variant
var rectSnap = rectTransform.Capture(); // DSMRectData
```

---

## Supported Types

| C# Type | JSON storage |
|---------|-------------|
| `int`, `long` | number |
| `float`, `double` | number |
| `bool` | boolean |
| `string` | string |
| `Vector2` | `{"x":0,"y":0}` |
| `Vector3` | `{"x":0,"y":0,"z":0}` |
| `Vector4` | `{"x":0,"y":0,"z":0,"w":0}` |
| `Quaternion` | `{"x":0,"y":0,"z":0,"w":1}` |
| `Color` | `{"r":1,"g":1,"b":1,"a":1}` |
| `Color32` | `{"r":255,"g":255,"b":255,"a":255}` |
| `DSMTransformData` | object |
| `DSMRectData` | object |

Any `[Serializable]` class is supported via Newtonsoft.Json.

---

## Editor Manager Window

Open via **DSM › Open Manager**.

```
┌────────────────────────────────────────────────────────────┐
│ DSM Manager                                     Refresh    │
├────────────────────────────────────────────────────────────┤
│ ▼ Configuration                                            │
│   Auto Save ☑  Debounce 2s  Encrypt ☐                     │
│   Default Slot  [default]              [Edit Config →]     │
├────────────────────────────────────────────────────────────┤
│ Slot: [ 001 ▼ ]  [+ New]  [Delete]                        │
├────────────────────────────────────────────────────────────┤
│ 🔍 Search...                             [+ New Entry]     │
│ ┌──────────────────────────────────────────────────────┐   │
│ │ hp          Int ▼                               [✕]  │   │
│ │ Default  [  100           ]                         │   │
│ │ Current  [  87            ]                         │   │
│ ├──────────────────────────────────────────────────────┤   │
│ │ speed       Float ▼                             [✕]  │   │
│ │ Default  [  3.5           ]                         │   │
│ │ Current  [  5.2           ]                         │   │
│ └──────────────────────────────────────────────────────┘   │
├────────────────────────────────────────────────────────────┤
│ 2 defaults · 2 in [001] · ● unsaved changes                │
│                              [Save DSMConstant.cs]         │
└────────────────────────────────────────────────────────────┘
```

### Entry layout (per row)

Each entry displays three lines:

| Line | Content |
|------|---------|
| Top | Key name · Type dropdown · Delete button |
| Default | Type-appropriate input — edits `DSMConstant.cs` (requires Save) |
| Current | Type-appropriate input — writes to the selected slot's JSON immediately |

### Columns

| Field | Source | Editable |
|-------|--------|---------|
| **Key** | DSMConstant (reflection) | Read-only |
| **Type** | DSMConstant | Yes — marks unsaved changes |
| **Default** | DSMConstant | Yes — marks unsaved changes; use **Save DSMConstant.cs** to write |
| **Current** | Selected slot's save file | Yes — writes to that slot's JSON immediately |

Default inputs are type-aware: Int/Float/Long show number fields, Bool shows a toggle, Vector2/3/4 show multi-axis fields, Color shows a color picker.

**Expose to Runtime Canvas:**
- Each entry has an **Expose** toggle in the header row
- When checked, a **Description** text field appears — this becomes the widget label at runtime
- Expose state and description are persisted in `DSMConfig.asset` immediately on change

### Workflows

**Add a new entry:**
1. Click **+ New Entry**
2. Enter Key, Type, and Default Value
3. Click **✚ Create** — adds the key with its default value to **all existing slot JSON files**, and marks DSMConstant as unsaved
4. Click **Save DSMConstant.cs** to write the constant and trigger recompile

**Remove an entry:**
- Click **✕** on any row → confirmation dialog → removes the key from **all slot JSON files** and marks DSMConstant as unsaved
- Click **Save DSMConstant.cs** to apply

**Edit a default value:**
- Change the Default field of any entry → status bar shows `● unsaved changes`
- Click **Save DSMConstant.cs** → file is written → Unity recompiles → window reloads automatically
- Existing slot values are not overwritten; only slots missing the key receive the new default

**Edit a current value:**
- Change the Current field → written to the selected slot's JSON immediately (no save button needed)

**Manage slots:**
- Dropdown to switch between existing slots
- **+ New** — creates a new slot pre-filled with all DSMConstant default values
- **Delete** — removes the active slot's save file (unavailable when only one slot exists)

**Add a constant manually in code:**
```csharp
// MyConstants.cs — your own partial file
public static partial class DSMConstant
{
    public const int bonusPoints = 500;
}
```
After compile, the window picks up `bonusPoints` automatically via reflection.

---

## Runtime Config Canvas

Expose selected DSM entries to an in-game UI canvas for live editing at runtime.

### Setup

**1. Create a Widget Config asset**

Right-click in Project → **DSM › Widget Config**. Assign a prefab for each type you want to support. Fields are typed — drag the prefab that has the matching widget component on it.

**2. Add DSMRuntimePanel to your Canvas**

Add the `DSMRuntimePanel` component to a GameObject inside your Canvas. Assign:

| Field | Value |
|-------|-------|
| `_config` | Your `DSMConfig.asset` |
| `_widgetConfig` | Your `DSMWidgetConfig.asset` |
| `_container` | A child `Transform` (e.g. a Vertical Layout Group) |
| `_slot` | Slot name to read/write (default: `"default"`) |

**3. Mark entries as Exposed**

In **DSM › Open Manager**, check the **Expose** toggle on any entry. Optionally fill in a **Description** — this becomes the label shown in the widget.

On `Start`, `DSMRuntimePanel` instantiates one widget per exposed entry into `_container`. Call `Rebuild()` at any time to re-generate widgets.

### Widget Contract

Each prefab must have a component implementing `IDSMWidget`:

```csharp
public interface IDSMWidget
{
    void Setup(string key, DSMDataType type, string label, DSMSlot slot);
}
```

`Setup` is called once per widget. Read the initial value with `slot.Get(key, defaultValue)` and write changes with `slot.Set(key, value)`.

### Built-in Widget Classes

Ready-to-use components in `Runtime/Widgets/` — attach to your prefabs:

| Class | UI Components needed | Type |
|-------|---------------------|------|
| `BoolWidget` | `TextMeshProUGUI` label + `Toggle` | Bool |
| `IntWidget` | `TextMeshProUGUI` label + `TMP_InputField` | Int |
| `FloatWidget` | `TextMeshProUGUI` label + `TMP_InputField` | Float |
| `DoubleWidget` | `TextMeshProUGUI` label + `TMP_InputField` | Double |
| `LongWidget` | `TextMeshProUGUI` label + `TMP_InputField` | Long |
| `StringWidget` | `TextMeshProUGUI` label + `TMP_InputField` | String |
| `Vector2Widget` | `TextMeshProUGUI` label + 2× `TMP_InputField` (X, Y) | Vector2 |
| `Vector3Widget` | `TextMeshProUGUI` label + 3× `TMP_InputField` (X, Y, Z) | Vector3 |
| `Vector4Widget` | `TextMeshProUGUI` label + 4× `TMP_InputField` (X, Y, Z, W) | Vector4 |
| `ColorWidget` | `TextMeshProUGUI` label + 4× `TMP_InputField` (R, G, B, A) | Color |

### Slot Behaviour

`DSMRuntimePanel` calls `DSM.GetSlot(_slot)` then `slot.Load()` on build:

| Condition | Result |
|-----------|--------|
| Slot exists with a save file | Loads data from disk — widgets show saved values |
| Slot name valid but no save file yet | Seeds from `DSMConstant` defaults |
| Slot name wrong/typo | Creates empty slot, seeds defaults — no crash, but widgets show defaults |

---

## Configuration via Code

DSM initializes automatically on first use — no setup call required. It loads `DSMConfig` from `Resources/DSMConfig.asset` and creates a default config if the asset doesn't exist.

`DSM.Configure()` is optional and only needed when you want to supply a config instance at runtime (e.g. loaded from a different path, or constructed in code):

```csharp
void Awake()
{
    DSM.Configure(myDSMConfig); // optional — overrides the auto-loaded config
}
```

Calling `Configure()` after DSM has already been used is safe; it replaces the manager cleanly.

---

## File Structure

```
Assets/DataSaveManager/
├── Runtime/
│   ├── DSM.cs                  Static API facade
│   ├── DSMConfig.cs            ScriptableObject configuration
│   ├── DSMSlot.cs              Single save slot (get/set/save/load/watch/seed-defaults)
│   ├── DSMSlotManager.cs       Multi-slot manager
│   ├── DSMSerializer.cs        JSON serialization with custom converters
│   ├── DSMEncryptor.cs         AES-256 encryption
│   ├── DSMWatcher.cs           Reactive change notifications
│   ├── DSMConstant.cs          Auto-generated typed constants (shared defaults)
│   ├── DSMDataEntry.cs         Entry model (key / type / default)
│   ├── DSMDataType.cs          Enum of supported types
│   ├── IDSMWidget.cs           Interface for runtime widget prefabs
│   ├── DSMWidgetConfig.cs      ScriptableObject — maps DSMDataType to widget prefab
│   ├── DSMRuntimePanel.cs      MonoBehaviour — spawns widgets on Canvas at runtime
│   ├── Types/
│   │   ├── DSMTransformData.cs
│   │   ├── DSMRectData.cs
│   │   └── Converters/         Newtonsoft.Json converters for Unity types
│   └── Widgets/
│       ├── BoolWidget.cs
│       ├── IntWidget.cs
│       ├── FloatWidget.cs
│       ├── DoubleWidget.cs
│       ├── LongWidget.cs
│       ├── StringWidget.cs
│       ├── Vector2Widget.cs
│       ├── Vector3Widget.cs
│       ├── Vector4Widget.cs
│       └── ColorWidget.cs
└── Editor/
    ├── DSMManagerWindow.cs     Editor window (includes Expose toggle per entry)
    ├── DSMCodeGenerator.cs     DSMConstant.cs code generation
    ├── DSMConstantSyncer.cs    Post-compile window refresh
    └── DSMConfigEditor.cs      DSMConfig inspector tools
```

---

## Dependencies

- [UniTask](https://github.com/Cysharp/UniTask) — async/await + `IUniTaskAsyncEnumerable`
- [Newtonsoft.Json for Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html) (`com.unity.nuget.newtonsoft-json`)
