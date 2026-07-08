# Architecture

**Analysis Date:** 2026-07-08

## System Overview

DataSaveManager is a layered save system for Unity that provides a simple static API for reading/writing typed game data across multiple save slots. The architecture separates concerns into public API, slot management, persistence (serialization/encryption), reactive watching, and runtime UI binding.

```text
┌─────────────────────────────────────────────────────────────┐
│                  Public API Layer                            │
│              `Runtime/DSM.cs` (static facade)                │
│         Set<T>(), Get<T>(), Save(), Load(), etc.            │
└────────────────┬─────────────────────────────────────────────┘
                 │
┌─────────────────▼─────────────────────────────────────────────┐
│            Slot Orchestration Layer                            │
│    `Runtime/DSMSlotManager.cs` — multi-slot management        │
│    Manages slot cache, active slot switching, slot discovery  │
└────────────────┬─────────────────────────────────────────────┘
                 │
┌─────────────────▼─────────────────────────────────────────────┐
│          Individual Slot Layer                                │
│    `Runtime/DSMSlot.cs` — single save slot                    │
│    In-memory key/value dictionary + reactive changes          │
├──────────────────┬──────────────────┬──────────────┬──────────┤
│                  │                  │              │          │
├──────────────────▼──────────────────▼──────────────▼──────────┤
│    Persistence & Serialization     │  Reactivity  │ Config   │
│  `DSMSerializer.cs`, Converters    │ `DSMWatcher` │ `DSM     │
│  `DSMEncryptor.cs` (AES-256)       │ (UniTask)    │ Config`  │
│  `DSMPaths.cs` (file discovery)    │              │          │
└─────────────────────────────────────┴──────────────┴──────────┘
         │                         │
         ▼                         ▼
    Disk I/O                 Runtime UI
 ({slot}.json/.enc)      `DSMRuntimePanel`
                         + `IDSMWidget` + Widgets
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| DSM | Static facade API; delegates to DSMSlotManager | `Runtime/DSM.cs` |
| DSMSlotManager | Manages slot cache, active slot, auto-initialization | `Runtime/DSMSlotManager.cs` |
| DSMSlot | Single slot: get/set in-memory data, save/load, watch changes | `Runtime/DSMSlot.cs` |
| DSMConfig | ScriptableObject configuration (auto-save, encryption, paths) | `Runtime/DSMConfig.cs` |
| DSMSerializer | JSON serialization with custom converters for Unity types | `Runtime/DSMSerializer.cs` |
| DSMEncryptor | AES-256 encryption with PBKDF2 key derivation, random per-file salt | `Runtime/DSMEncryptor.cs` |
| DSMWatcher | Reactive change notification using UniTask channels | `Runtime/DSMWatcher.cs` |
| DSMRuntimePanel | Spawns UI widgets from DSMConfig exposed entries at runtime | `Runtime/DSMRuntimePanel.cs` |
| IDSMWidget | Interface for type-specific widget implementations | `Runtime/IDSMWidget.cs` |
| DSMWidgetConfig | Maps DSMDataType to widget prefab MonoBehaviours | `Runtime/DSMWidgetConfig.cs` |
| DSMConstant | Auto-generated typed constants (shared default values) | `Runtime/DSMConstant.cs` |
| DSMCodeGenerator | Generates DSMConstant.cs from editor-defined entries | `Editor/DSMCodeGenerator.cs` |
| DSMManagerWindow | Editor window for managing entries, slots, defaults, and expose state | `Editor/DSMManagerWindow.cs` |
| DSMConstantSyncer | Post-compile hook to refresh manager window when DSMConstant changes | `Editor/DSMConstantSyncer.cs` |

## Pattern Overview

**Overall:** Layered architecture with facade pattern (DSM) providing a simple synchronous API over an asynchronous, async-enumerable-based internals.

**Key Characteristics:**
- **Lazy initialization:** DSMSlotManager is created on first DSM.* call, loads DSMConfig from Resources
- **Shared defaults:** All slots seed from DSMConstant (static fields via reflection) on first load
- **Reactive via UniTask:** Watchers use UniTask channels for change notifications
- **Dual persistence paths:** Supports both JSON (unencrypted) and `.enc` (AES-256 encrypted) files
- **Type-safe API:** Generics throughout (Set<T>, Get<T>, WatchAsync<T>) with no magic strings required when using DSMConstant

## Layers

**Public API Layer:**
- Purpose: Simple static API for game code to get/set/save/load data
- Location: `Runtime/DSM.cs`
- Contains: Static methods routing to active DSMSlot
- Depends on: DSMSlotManager (created lazily)
- Used by: All game code, UI controllers, gameplay systems

**Slot Management Layer:**
- Purpose: Manage multiple save slots and active slot switching
- Location: `Runtime/DSMSlotManager.cs`
- Contains: Slot dictionary, active slot tracking, reflection-based DSMConstant discovery
- Depends on: DSMConfig, DSMSlot, DSMSerializer, DSMPaths
- Used by: DSM facade

**Individual Slot Layer:**
- Purpose: In-memory key/value storage for one save slot
- Location: `Runtime/DSMSlot.cs`
- Contains: Dictionary<string, JToken> _data, auto-save debounce scheduling, file load/save
- Depends on: DSMConfig, DSMSerializer, DSMEncryptor, DSMWatcher
- Used by: DSMSlotManager, DSMRuntimePanel, game code (via DSM)

**Serialization & Persistence Layer:**
- Purpose: Convert between in-memory objects and JSON/encrypted bytes
- Locations: `Runtime/DSMSerializer.cs`, `Runtime/DSMEncryptor.cs`, `Runtime/Types/Converters/*`
- Contains: Newtonsoft.Json setup with custom converters (Vector3, Quaternion, Color, etc.)
- Depends on: Nothing (utility layer)
- Used by: DSMSlot during save/load

**Reactivity Layer:**
- Purpose: Notify subscribers of value changes via async streams
- Location: `Runtime/DSMWatcher.cs`
- Contains: Dictionary of UniTask channels per key, thread-safe registration
- Depends on: UniTask
- Used by: DSMSlot when Set() is called; consumed by WatchAsync<T> subscribers

**Configuration Layer:**
- Purpose: Store and manage configuration (auto-save, encryption, paths, exposed entries)
- Location: `Runtime/DSMConfig.cs`
- Contains: ScriptableObject properties + ExposedEntry list
- Depends on: Nothing (data holder)
- Used by: DSMSlotManager, DSMSlot, DSMRuntimePanel, Editor tools

**Runtime UI Layer:**
- Purpose: Dynamically instantiate and bind UI widgets to DSM values
- Locations: `Runtime/DSMRuntimePanel.cs`, `Runtime/IDSMWidget.cs`, `Runtime/Widgets/*`, `Runtime/DSMWidgetConfig.cs`
- Contains: MonoBehaviour for widget spawning, interface contract, type-specific implementations (IntWidget, BoolWidget, etc.)
- Depends on: DSMSlot, DSMConfig, DSMDataType, TextMeshPro, Unity UI
- Used by: Game scenes that want in-game configuration UI

**Editor Tools Layer:**
- Purpose: UI and code generation for defining defaults, managing slots, and syncing constant definitions
- Locations: `Editor/DSMManagerWindow.cs`, `Editor/DSMCodeGenerator.cs`, `Editor/DSMConstantSyncer.cs`, `Editor/DSMConfigEditor.cs`
- Contains: EditorWindow, reflection-based entry discovery, code generation templates
- Depends on: DSMConfig, DSMDataEntry, DSMDataType, Unity Editor
- Used by: Developers in the Editor

## Data Flow

### Primary Request Path (Set → WatchAsync)

1. **Game code calls DSM.Set()** → `Runtime/DSM.cs:39`
   - Routes to `DSMSlotManager.ActiveSlot.Set(key, value)`
2. **DSMSlot.Set()** → `Runtime/DSMSlot.cs:32`
   - Converts value to JToken using JsonSerializer
   - Stores in `_data[key]`
3. **DSMWatcher.Notify()** → `Runtime/DSMSlot.cs:35`
   - Notifies all listeners watching that key
4. **DSMWatcher channels emit** → `Runtime/DSMWatcher.cs:42-52`
   - Async streams (WatchAsync subscribers) receive new value immediately
5. **AutoSave debounce triggered** → `Runtime/DSMSlot.cs:36-37`
   - If enabled, schedules SaveAsync() with delay
6. **Debounced SaveAsync()** → `Runtime/DSMSlot.cs:145-149`
   - Waits N seconds, then calls SaveAsync()
7. **SaveAsync() executes** → `Runtime/DSMSlot.cs:80-90`
   - Serializes _data to JSON
   - Optionally encrypts with DSMEncryptor
   - Writes to disk

### Load Path (UseSlot → SeedDefaults)

1. **Game code calls DSM.UseSlot("slot_name")** → `Runtime/DSM.cs:24`
2. **DSMSlotManager.UseSlot()** → `Runtime/DSMSlotManager.cs:27-31`
   - Gets or creates DSMSlot
   - Calls slot.Load()
3. **DSMSlot.Load()** → `Runtime/DSMSlot.cs:64-78`
   - Checks disk for saved file (prefers .enc over .json)
   - If found: decrypts (if needed) + deserializes JSON → _data dictionary
   - If not found: SeedDefaults()
4. **SeedDefaults()** → `Runtime/DSMSlot.cs:123-134`
   - Uses reflection to read all static fields from DSMConstant
   - Populates _data with default values

### Expose to Runtime UI Path

1. **Editor: Developer marks entry as "Exposed" in DSMManagerWindow** → `Editor/DSMManagerWindow.cs`
   - Calls DSMConfig.SetExposed() → saves to asset
2. **Runtime: DSMRuntimePanel.Awake()** → `Runtime/DSMRuntimePanel.cs:15`
   - Reads DSMConfig.ExposedEntries
   - For each entry, looks up widget prefab from DSMWidgetConfig
   - Instantiates prefab into _container
3. **Widget.Setup()** → `Runtime/Widgets/IntWidget.cs:14-26`
   - Reads current value from slot: `slot.Get(key, default)`
   - Populates UI with value
   - Registers onEndEdit callback
4. **User edits widget** → `Runtime/Widgets/IntWidget.cs:28-32`
   - Apply() parses input, calls `slot.Set(key, value)`
   - Change notification flows through DSMWatcher (no UI needed here)

**State Management:**
- In-memory state: Dictionary<string, JToken> in DSMSlot
- Persisted state: JSON or encrypted binary files on disk at `{persistentDataPath}/DSM/{slotName}.json|.enc`
- Defaults: Static fields in DSMConstant (auto-generated)
- Exposed UI mappings: List<ExposedEntry> in DSMConfig asset

## Key Abstractions

**DSM.Set<T> / DSM.Get<T>:**
- Purpose: Simple typed API hiding generics, slot management, and serialization
- Examples: `DSM.Set("hp", 100)`, `DSM.Get("hp", 50)`
- Pattern: Static facade delegating to ActiveSlot

**DSMSlot:**
- Purpose: Self-contained save slot with load/save, in-memory cache, and watch support
- Each slot is independent; data never leaks between slots
- Load() seeds missing keys from DSMConstant defaults
- WatchAsync() yields current value immediately, then future changes

**DSMWatcher + UniTask Channels:**
- Purpose: Decouple change notifications from subscribers
- Each key has a list of channels; Notify() broadcasts to all
- Thread-safe registration/unregistration via lock
- Subscribers receive values as async stream, can use WithCancellation()

**DSMEncryptor (Static Utility):**
- Purpose: Transparent AES-256 encryption/decryption
- File format: [16-byte IV][32-byte salt][ciphertext]
- Random salt per file prevents rainbow-table attacks
- PBKDF2 key derivation from password with 10,000 iterations

**IDSMWidget Interface:**
- Purpose: Contract for all widget implementations
- Setup(key, type, label, slot) called once per widget instance
- Each widget reads/writes to slot directly, no central mediator needed
- Type-specific widgets (IntWidget, BoolWidget, Vector3Widget) implement same interface

**DSMConstant (Auto-Generated):**
- Purpose: Typed constants that serve as defaults for all slots
- Generated by DSMCodeGenerator from editor-defined entries
- Reflection-based discovery in DSMSlotManager/DSMSlot
- Allows code like: `DSM.Get(nameof(DSMConstant.hp), DSMConstant.hp)`

## Entry Points

**Runtime Initialization:**
- Location: `Runtime/DSM.cs:76-84` (Initialize method)
- Triggers: First call to any DSM.* method
- Responsibilities: Load DSMConfig from Resources, create DSMSlotManager, hook Application.quitting

**Game Code Integration:**
- Primary: `DSM.Set<T>(key, value)` and `DSM.Get<T>(key, defaultValue)`
- Async: `await DSM.SaveAsync()`, `await DSM.LoadAsync()`
- Reactive: `await foreach (var hp in DSM.WatchAsync<int>("hp")) { ... }`
- Slot switching: `DSM.UseSlot("slot_name")`

**Editor Integration:**
- Menu: `DSM › Create Config Asset` → creates DSMConfig in Resources
- Menu: `DSM › Open Manager` → opens DSMManagerWindow
- Menu: `DSM › Create Widget Config` → creates DSMWidgetConfig asset

**Runtime UI Integration:**
- Attach `DSMRuntimePanel` to a Canvas GameObject
- Assign DSMConfig, DSMWidgetConfig, and container Transform
- On Awake, panel spawns widgets for all exposed entries

## Architectural Constraints

- **Threading:** Single-threaded event loop (Unity main thread only); DSMWatcher uses lock for multi-writer safety on channels
- **Global state:** `DSM.s_manager` is static singleton; `DSMConstant` is class of static fields; both initialized lazily
- **Circular imports:** None detected; dependency graph is acyclic (DSM → DSMSlotManager → DSMSlot → Serialization/Encryption/Watcher)
- **Mutable state:** DSMSlot._data is mutable Dictionary; modified in-place by Set(), Load(), SeedDefaults()
- **UniTask dependency:** Core reactivity depends on UniTask channels; Set() implementations that don't subscribe to WatchAsync() have no async cost
- **File I/O:** Synchronous (Save()) and asynchronous (SaveAsync()) both supported; LoadAsync() must be called explicitly to get async behavior
- **Memory:** Entire slot loaded into memory as Dictionary; suitable for single-player games, not for large distributed databases

## Anti-Patterns

### Global Mutable Manager Instance

**What happens:** `DSMSlotManager` is instantiated once and cached in `DSM.s_manager` static field.

**Why it's wrong:** Makes testing harder; couples all code to singleton pattern; makes it impossible to have independent DSM instances per context.

**Do this instead:** Code already has Configure() method at `Runtime/DSM.cs:13-19` allowing replacement. Use it in tests: `DSM.Configure(testConfig)` to inject a test manager. For production, lazy initialization is fine; just ensure tests explicitly call Configure() with test instances.

### Magic String Keys Without DSMConstant

**What happens:** Code uses `DSM.Set("hp", 100)` with hardcoded key strings, which breaks silently if key is misspelled.

**Why it's wrong:** Typos create new keys instead of updating intended keys; no compile-time safety.

**Do this instead:** Always use `DSMConstant`. Define entry in Manager window, generate constant, then use: `DSM.Set(nameof(DSMConstant.hp), 100)`. This provides compile-time safety and auto-complete.

## Error Handling

**Strategy:** Silent fallbacks with warnings where appropriate

**Patterns:**
- `DSMSlot.SeedDefaults()` catches exceptions per field and logs warnings; missing default doesn't crash
- `DSMSlotManager.ResolveConstantType()` catches ReflectionTypeLoadException and returns null; DSM works without constants
- `DSMEncryptor` throws on decryption failure; calling code must handle (typically in Load())
- File I/O failures propagate; calling code must handle in try/catch if needed
- WatchAsync unregisters automatically on cancellation; no manual cleanup needed

## Cross-Cutting Concerns

**Logging:** No logger dependency; uses `Debug.Log` and `Debug.LogWarning` for diagnostic messages

**Validation:** DSMSlot.Set() requires T : notnull; DSMSerializer.Deserialize() uses Newtonsoft's null handling (returns null defaultValue if key missing)

**Authentication:** No built-in auth; encryption via DSMEncryptor provides security (password-based)

---

*Architecture analysis: 2026-07-08*
