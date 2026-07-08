# Codebase Structure

**Analysis Date:** 2026-07-08

## Directory Layout

```
Assets/DataSaveManager/
├── Runtime/                          # Core runtime library
│   ├── DSM.cs                        # Static facade API
│   ├── DSMSlotManager.cs             # Multi-slot orchestration
│   ├── DSMSlot.cs                    # Single save slot
│   ├── DSMConfig.cs                  # Configuration ScriptableObject
│   ├── DSMSerializer.cs              # JSON serialization
│   ├── DSMEncryptor.cs               # AES-256 encryption
│   ├── DSMWatcher.cs                 # Reactive change notification
│   ├── DSMConstant.cs                # Auto-generated defaults (created by editor tool)
│   ├── DSMDataEntry.cs               # Entry model for editor
│   ├── DSMDataType.cs                # Enum of supported types
│   ├── DSMPaths.cs                   # Save directory path resolution
│   ├── IDSMWidget.cs                 # Widget interface contract
│   ├── DSMWidgetConfig.cs            # Widget-to-type mappings
│   ├── DSMRuntimePanel.cs            # Runtime widget instantiation
│   ├── Types/                        # Helper data types
│   │   ├── DSMTransformData.cs       # Position/rotation/scale snapshot
│   │   ├── DSMRectData.cs            # RectTransform snapshot
│   │   └── Converters/               # Custom JSON converters for Unity types
│   │       ├── Vector2Converter.cs
│   │       ├── Vector3Converter.cs
│   │       ├── Vector4Converter.cs
│   │       ├── QuaternionConverter.cs
│   │       ├── ColorConverter.cs
│   │       └── Color32Converter.cs
│   ├── Widgets/                      # Type-specific widget implementations
│   │   ├── BoolWidget.cs
│   │   ├── IntWidget.cs
│   │   ├── FloatWidget.cs
│   │   ├── DoubleWidget.cs
│   │   ├── LongWidget.cs
│   │   ├── StringWidget.cs
│   │   ├── Vector2Widget.cs
│   │   ├── Vector3Widget.cs
│   │   ├── Vector4Widget.cs
│   │   └── ColorWidget.cs
│   └── DMS.Runtime.asmdef            # Assembly definition for runtime
├── Editor/                           # Editor-only tools
│   ├── DSMManagerWindow.cs           # Main editor window (entries, slots, defaults)
│   ├── DSMCodeGenerator.cs           # Generates DSMConstant.cs source
│   ├── DSMConstantSyncer.cs          # Post-compile refresh hook
│   ├── DSMConfigEditor.cs            # Custom Inspector for DSMConfig
│   └── DSM.Editor.asmdef             # Assembly definition for editor tools
├── Prefab/                           # Optional reference prefabs
│   └── (widget prefab examples or demo scenes)
├── README.md                         # Full documentation and API reference
├── package.json                      # Unity package manifest
└── .git/                             # Git repository (separate from counter-stack-game)
```

## Directory Purposes

**Runtime/ (Core Library):**
- Purpose: Game-facing save system implementation
- Contains: API facade, slot management, persistence, reactivity, type system
- Key files: `DSM.cs`, `DSMSlot.cs`, `DSMSlotManager.cs`

**Runtime/Types/:**
- Purpose: Domain models and serialization helpers
- Contains: `DSMTransformData` (transform snapshot), `DSMRectData` (RectTransform snapshot), custom JSON converters
- Why separate: Converters are isolated; can be extended without modifying core slot logic

**Runtime/Types/Converters/:**
- Purpose: Enable serialization of Unity math types (Vector3, Quaternion, Color, etc.)
- Contains: Newtonsoft.Json JsonConverter implementations per type
- Pattern: Each file handles one type's WriteJson/ReadJson

**Runtime/Widgets/:**
- Purpose: Pre-built UI widget components for runtime configuration panel
- Contains: IntWidget, FloatWidget, BoolWidget, Vector3Widget, etc. — one per data type
- Pattern: Each widget implements `IDSMWidget` interface and handles its type's UI binding

**Editor/:**
- Purpose: Development-time tools for defining constants and managing data
- Contains: `DSMManagerWindow` (UI for entry/slot management), code generation, post-compile syncing
- Why separate: Editor-only code; excluded from runtime builds via UNITY_EDITOR guards

## Key File Locations

**Entry Points:**

| File | Purpose |
|------|---------|
| `Runtime/DSM.cs` | Public static API; game code starts here |
| `Runtime/DSMSlotManager.cs` | Initialized on first DSM.* call (lazy singleton) |
| `Editor/DSMManagerWindow.cs` | Opened via menu "DSM › Open Manager" |

**Configuration:**

| File | Purpose |
|------|---------|
| `Runtime/DSMConfig.cs` | Settings asset (auto-save, encryption, paths); created at `Resources/DSMConfig.asset` |
| `Runtime/DSMWidgetConfig.cs` | Widget prefab mappings; created manually by developer |

**Core Logic:**

| File | Purpose |
|------|---------|
| `Runtime/DSMSlot.cs` | Individual save slot logic (get/set/load/save/watch) |
| `Runtime/DSMSerializer.cs` | JSON serialization with custom converters |
| `Runtime/DSMEncryptor.cs` | AES-256 encryption/decryption utility |
| `Runtime/DSMWatcher.cs` | Reactive change notifications via UniTask channels |

**Type System:**

| File | Purpose |
|------|---------|
| `Runtime/DSMDataType.cs` | Enum: Int, Float, Double, Long, Bool, String, Vector2, Vector3, Vector4, Color |
| `Runtime/DSMDataEntry.cs` | Editor model: key/type/default-value tuple |
| `Runtime/DSMConstant.cs` | Auto-generated typed constants (defaults) |

**Runtime UI:**

| File | Purpose |
|------|---------|
| `Runtime/DSMRuntimePanel.cs` | MonoBehaviour that spawns widgets on Canvas |
| `Runtime/IDSMWidget.cs` | Interface contract: Setup(key, type, label, slot) |
| `Runtime/Widgets/*.cs` | Implementations: IntWidget, BoolWidget, Vector3Widget, etc. |
| `Runtime/DSMWidgetConfig.cs` | Maps DSMDataType → widget prefab component |

**Editor Tools:**

| File | Purpose |
|------|---------|
| `Editor/DSMManagerWindow.cs` | EditorWindow: define entries, manage slots, edit defaults/current values, mark exposed |
| `Editor/DSMCodeGenerator.cs` | Generates `DSMConstant.cs` C# source file from editor-defined entries |
| `Editor/DSMConstantSyncer.cs` | Post-compile hook to refresh Manager window when constants change |
| `Editor/DSMConfigEditor.cs` | Custom Inspector tools for DSMConfig (if any) |

**Persistent Data Storage:**

Location: `{Application.persistentDataPath}/DSM/`
- `{slotName}.json` — unencrypted JSON save file
- `{slotName}.enc` — encrypted AES-256 save file (binary format: [IV][salt][ciphertext])

## Naming Conventions

**Files:**
- Prefix: `DSM` — all files prefixed with `DSM` (DSMSlot.cs, DSMWatcher.cs, etc.)
- Pattern: `DSM{ComponentName}.cs` (e.g., DSMSerializer.cs, DSMEncryptor.cs)
- Widget files: `{TypeName}Widget.cs` (e.g., IntWidget.cs, Vector3Widget.cs)
- Converters: `{TypeName}Converter.cs` (e.g., Vector3Converter.cs, ColorConverter.cs)

**Directories:**
- Flat for core files: `Runtime/*.cs` (no nested folders for DSM*.cs files)
- Organized by function:
  - `Runtime/Types/` — data type models
  - `Runtime/Types/Converters/` — JSON converters (one per Unity type)
  - `Runtime/Widgets/` — UI widget implementations
  - `Editor/` — editor-only tools

**Classes/Interfaces:**
- PascalCase: `DSMSlotManager`, `IDSMWidget`
- Static facades: `DSM` (no prefix), `DSMEncryptor` (utility, static methods)
- Private fields: `_camelCase` (e.g., `_data`, `_config`, `_watcher`)
- Public properties: `PascalCase` with get-only accessors (e.g., `ActiveSlot`, `SaveDirectory`)

**Methods:**
- Public: `PascalCase` (Set, Get, Save, Load, WatchAsync, UseSlot)
- Private: `camelCase` (seedDefaults, scheduleSave, getLoadPath)

## Where to Add New Code

### New Feature (e.g., "add compression support")

**Primary code:** `Runtime/DSM{Feature}.cs` or extend existing slot management
- Example: `Runtime/DSMCompressor.cs` for data compression
- Call from: `DSMSlot.cs` during Save/Load

**Tests:** (if test project exists) `Tests/Runtime/DSM{Feature}.Tests.cs`

**Configuration:** If new config needed, extend `Runtime/DSMConfig.cs` with new serialized field

**Example path:** `Runtime/DSMCompressor.cs` → used by `DSMSlot.cs` before/after encryption

### New Data Type (e.g., "support Guid")

1. Add to `DSMDataType.cs` enum
2. Create `Runtime/Types/Converters/GuidConverter.cs` (inherit JsonConverter<Guid>)
3. Register in `DSMSerializer.__init()`: `JsonSerializer.Converters.Add(new GuidConverter())`
4. Create `Runtime/Widgets/GuidWidget.cs` (implement IDSMWidget)
5. Add field to `DSMWidgetConfig.cs`: `[SerializeField] private GuidWidget _guidWidget;`
6. Update switch in `GetWidgetPrefab()` method
7. Update `Editor/DSMManagerWindow.cs` UI to support Guid input (if adding new entry type)

### New Widget Type (to expose Guid in runtime UI)

1. File: `Runtime/Widgets/GuidWidget.cs`
2. Pattern (template from `Runtime/Widgets/IntWidget.cs`):
   ```csharp
   public sealed class GuidWidget : MonoBehaviour, IDSMWidget
   {
       [SerializeField] private TextMeshProUGUI _label;
       [SerializeField] private TMP_InputField _input;
       private string _key;
       private DSMSlot _slot;

       public void Setup(string key, DSMDataType type, string label, DSMSlot slot)
       {
           _key = key;
           _slot = slot;
           _label.text = label;
           _input.text = slot.Get(key, Guid.Empty).ToString();
           _input.onEndEdit.AddListener(_ => Apply());
       }

       private void Apply()
       {
           if (Guid.TryParse(_input.text, out var guid))
               _slot.Set(_key, guid);
       }
   }
   ```
3. Attach to prefab; assign TMP components in Inspector
4. Register in `DSMWidgetConfig.cs`

### New Editor Tool

1. File: `Editor/DSM{ToolName}.cs`
2. Guard with `#if UNITY_EDITOR` / `#endif`
3. Use `[MenuItem("DSM/...")]` for menu items
4. Example: `Editor/DSMConfigEditor.cs` for custom Inspector behavior
5. If needs post-compile hook: inherit from `IPostprocessBuildWithReport` or use `InitializeOnLoad`

### New Serialized Helper Type (like DSMTransformData)

1. File: `Runtime/Types/DSM{ComponentName}Data.cs`
2. Mark struct/class as `[Serializable]`
3. Add extension methods if needed (see DSMTransformExtensions in DSMTransformData.cs)
4. Register converter in `DSMSerializer.__init()` if custom JSON format needed
5. Optionally add to DSMDataType enum and widget if exposing in runtime UI

## Special Directories

**Prefab/:**
- Purpose: Reference/example prefabs showing widget setup
- Generated: No
- Committed: Yes
- Content: Example DSMRuntimePanel setup with sample widgets

**Resources/:**
- Purpose: Auto-loaded DSMConfig asset location
- Generated: No (created by user via editor menu)
- Committed: Optionally (consider version control strategy for local save states)
- Path: `Assets/Resources/DSMConfig.asset` or `Assets/Resources/DSMWidgetConfig.asset`

**Save Files Directory:**
- Purpose: Player save files (slots)
- Generated: Yes (by DSMSlot.Save/SaveAsync)
- Committed: No (player data, typically .gitignored)
- Path: `{Application.persistentDataPath}/DSM/{slotName}.json|.enc`

## Architecture-Specific Patterns to Follow

**New Slot-Related Logic:**
- Add method to `DSMSlot.cs` (e.g., new persistence feature)
- Keep method public only if game code should call it
- Use existing dependencies (DSMSerializer, DSMEncryptor, DSMWatcher)
- Example: Add `TransferSlot(sourceName, destName)` to `DSMSlotManager` for copy operations

**New Public API Methods:**
- Add to `DSM.cs` static class only
- Delegate to `Manager.ActiveSlot` or `Manager` as appropriate
- Examples: `DSM.Has(key)`, `DSM.Delete(key)` already follow this pattern

**New Type Support:**
- Must support: (1) Converter to/from JSON, (2) Widget for UI, (3) Entry in DSMDataType enum
- Add all three together to keep system consistent
- Follow existing type patterns (see IntWidget, Vector3Converter for templates)

**Custom Configuration:**
- Store in `DSMConfig.cs` as `[SerializeField]` with backing property
- Mark non-serializable data as `[field: NonSerialized]` (e.g., EncryptionKey)
- Expose via inspector-friendly properties (PascalCase getters)

---

*Structure analysis: 2026-07-08*
