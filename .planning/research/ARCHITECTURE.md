# Architecture Research

**Domain:** Unity save-system hardening (existing layered architecture, incremental milestone)
**Researched:** 2026-07-08
**Confidence:** HIGH (internal architecture reasoning, direct codebase read) / LOW (external "best practice" pattern validation — general web search, no authoritative single source; treat as corroborating, not load-bearing)

## Context: What We're Integrating Into

This is not greenfield research — DSM already has a working layered architecture (`DSM` → `DSMSlotManager` → `DSMSlot` → `DSMSerializer`/`DSMEncryptor`/`DSMWatcher`), documented in `.planning/codebase/ARCHITECTURE.md`. The milestone adds four hardening capabilities on top of it:

1. Thread-safety for `DSMSlot._data`
2. Schema validation (type-safety on `Set`/`Get`)
3. Slot versioning + migration
4. Encryption key rotation

The governing constraint from `PROJECT.md`: **no backward compatibility required** for old save files or keys — so file-format changes are allowed, but the **public C# API surface** (`DSM.Set<T>`, `DSM.Get<T>`, `DSM.SaveAsync()`, etc.) should change as little as possible. All four features should be additive to the existing layer boundaries, not a re-layering.

## Standard Architecture (Target State)

```text
┌─────────────────────────────────────────────────────────────────┐
│                     Public API Layer                             │
│                  `Runtime/DSM.cs` (static facade)                │
│   Set<T>/Get<T> unchanged; + DSM.RotateEncryptionKeyAsync()      │
└───────────────────────────┬────────────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────────────┐
│                Slot Orchestration Layer                          │
│         `Runtime/DSMSlotManager.cs`                              │
│  + ConcurrentDictionary<string,DSMSlot> _slots (was Dictionary)  │
│  + RotateEncryptionKeyAsync(newKey) — iterates cached slots,     │
│    delegates the actual re-encrypt cycle to each DSMSlot         │
└───────────────────────────┬────────────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────────────┐
│                  Individual Slot Layer                           │
│              `Runtime/DSMSlot.cs`                                │
│  + ConcurrentDictionary<string,JToken> _data (was Dictionary)    │
│  + object/SemaphoreSlim _ioGate for atomic Load/Save/Rotate      │
│  Set()/Get() now call into DSMSchema before mutating _data       │
│  Load()/Save() now wrap payload in a versioned envelope and      │
│  call DSMMigrationRunner when loaded version < current version   │
├──────────────┬──────────────┬──────────────┬─────────┬──────────┤
│              │              │              │         │          │
├──────────────▼──────────────▼──────────────▼─────────▼──────────┤
│  Persistence  │  Schema      │  Migration   │Reactivity│ Config  │
│ DSMSerializer │ DSMSchema    │ DSMMigration │DSMWatcher│DSMConfig│
│ DSMEncryptor  │ (NEW)        │ Runner (NEW) │(existing)│+SaveVer │
│ DSMPaths      │              │ + IDSMMigra- │          │sion,    │
│               │              │   tion (NEW) │          │StrictSc │
│               │              │              │          │hema flag│
└───────────────┴──────────────┴──────────────┴──────────┴─────────┘
         │                         │
         ▼                         ▼
    Disk I/O                 Runtime UI / Editor UI
 ({slot}.json/.enc,      DSMRuntimePanel (unchanged)
  now envelope-wrapped)  DSMManagerWindow (NEW: version/migration/
                          rotate-key panel — as a separate class,
                          not appended to the existing 825-line file)
```

## Component Responsibilities (New/Changed Only)

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| `DSMSchema` (NEW, `Runtime/`) | Holds key→expected-`DSMDataType` map; validates `Set<T>`/`Get<T>` calls against it | Pure static/utility class, built once per slot from `DSMDataEntry` metadata (already exists for editor entries) — no Unity API dependency, unit-testable in EditMode |
| `IDSMMigration` (NEW, `Runtime/`) | Contract for one version-to-version transform | `int FromVersion { get; }`, `int ToVersion { get; }`, `JObject Migrate(JObject data)` |
| `DSMMigrationRunner` (NEW, `Runtime/`) | Applies ordered `IDSMMigration` steps until envelope version == `DSMConfig.SaveVersion` | Pure logic over `JObject`; no file I/O — called by `DSMSlot.Load()` after deserialize, before `_data` population |
| `DSMSlot` (CHANGED) | Adds thread-safe `_data`, an I/O gate for atomic Load/Save/Rotate, envelope read/write, schema enforcement | `ConcurrentDictionary` + `SemaphoreSlim`/`lock` for compound ops |
| `DSMSlotManager` (CHANGED) | Adds `RotateEncryptionKeyAsync(newKey)`; thread-safe slot cache | `ConcurrentDictionary<string, DSMSlot>` instead of `Dictionary`; delegates rotation mechanics to `DSMSlot` |
| `DSMConfig` (CHANGED) | New fields: `SaveVersion` (int), `StrictSchema` (bool, controls throw-vs-warn on mismatch) | Still a pure data holder — no new logic |
| `DSMManagerWindow` companion (NEW, `Editor/`) | Surfaces per-slot stored version, migration status, "Rotate Key" action, schema validation errors | New file(s), *not* appended to `DSMManagerWindow.cs` — reuses `DSMSlotManager`/`DSMSchema`/`DSMMigrationRunner` rather than duplicating logic in the editor layer |

## Component Boundary Implications

**1. Thread-safety is internal, not a new layer.**
`_data` becoming a `ConcurrentDictionary` and adding an I/O gate around `Load`/`Save`/`Rotate` is entirely inside `DSMSlot`. No public API changes. `DSMSlotManager._slots` needs the same treatment — `GetOrCreateSlot()`/`UseSlot()` can, in principle, be reached from a `UniTask` continuation that has resumed off the Unity main thread (e.g. after `.ConfigureAwait(false)` inside library code, or a background `SaveAsync` chain), so slot-cache mutation must be as safe as slot-data mutation. Treat "thread-safety" as one hardening unit spanning both `DSMSlot` and `DSMSlotManager`, not just the `_data` dictionary in isolation.

**2. Schema validation slots into the existing `Set`/`Get` call path, not a new layer above `DSM`.**
`DSMSchema` is a new collaborator for `DSMSlot`, at the same tier as `DSMSerializer`/`DSMEncryptor` (utility, no upstream dependents besides `DSMSlot`). It must **not** live in `DSM.cs` (the facade should stay a thin delegator) and must **not** be built as another responsibility bolted onto `DSMManagerWindow.cs` — the type metadata it needs (`DSMDataEntry.Type`) already flows from the editor into `DSMConstant`/config data, so `DSMSchema` should consume that existing metadata rather than the editor scanning for it a second time.

**3. Migration changes the on-disk contract, not the in-memory or public API contract.**
The `.json`/`.enc` payload becomes an envelope: `{ "version": <int>, "data": { ...existing per-key JToken map... } }`. This is exactly the kind of breaking-but-acceptable change `PROJECT.md` sanctions ("Out of Scope: backward compatibility with old save files"). `DSMMigrationRunner` sits strictly between deserialize and `_data` population inside `DSMSlot.Load()` — it never talks to `DSMSlotManager` or `DSM` directly. Because migrations transform a `JObject` toward "whatever `DSMSchema` currently considers valid," schema validation is a **soft prerequisite**: migrations should be verifiable against the schema, so the schema contract needs to exist (even if minimal) before migration is meaningful to test.

**4. Key rotation is an orchestration concern, not a new persistence primitive.**
Rotation should not invent a parallel encrypt/decrypt path. `DSMSlotManager.RotateEncryptionKeyAsync(newKey)` should: for each cached (or discoverable) slot, `Load()` with the current key, hold the I/O gate, `Save()` with the new key (reusing `DSMEncryptor`'s existing per-file random salt/IV, which is already correct), then update `DSMConfig.EncryptionKey`. Given DSM is a **single local encryption key per config** (not a multi-tenant key-management system), the enterprise pattern of a key-version header + "retired keys stay decrypt-only indefinitely" (surfaced in the web research below) is over-engineering for this project's scope — a straightforward decrypt-old/re-encrypt-new pass covers the stated requirement ("security best practices, periodic key rotation") without adding a second persistent-state concept.

**5. Editor tooling boundary — reinforce, don't erode, the split already planned in CONCERNS.md.**
`DSMManagerWindow.cs` is already flagged as an 825-line god-class slated for decomposition. Every new admin surface this milestone needs (show stored slot version, show pending migrations, "Rotate Key" button, display schema validation errors) is new UI responsibility. Route it through new dedicated editor classes that call into `DSMSlotManager`/`DSMSchema`/`DSMMigrationRunner`, rather than adding methods to the existing window class. This makes the god-class refactor (already in scope) and the new hardening features mutually reinforcing instead of competing.

**6. Public API surface delta (additive only).**
- `DSM.Set<T>`/`DSM.Get<T>` — signatures unchanged; behavior changes only in failure mode (mismatch now validated, controlled by `DSMConfig.StrictSchema`: throw in strict mode, warn+coerce/ignore in lenient mode — keep a lenient default so existing call sites don't suddenly start throwing).
- New: `DSM.RotateEncryptionKeyAsync(string newKey)` (facade) → `DSMSlotManager.RotateEncryptionKeyAsync(string newKey)`.
- New: `DSMConfig.SaveVersion` (int), `DSMConfig.StrictSchema` (bool).
- New: `IDSMMigration` extension point for consumers who need custom per-key transforms across versions.
- No changes to `DSMWatcher`/`WatchAsync<T>` — schema/version/rotation are orthogonal to reactive notification.

## Data Flow (New/Changed Paths)

### Set/Get with Schema Validation

```
Game code: DSM.Set(key, value)
    → DSMSlotManager.ActiveSlot.Set(key, value)
        → DSMSchema.Validate(key, typeof(value))     [NEW step]
            - key known + type matches  → proceed
            - key known + type mismatch → throw (strict) or warn+coerce (lenient)
            - key unknown                → proceed (new key, no schema entry yet) or warn, per config
        → _data[key] = JToken.FromObject(value)        [ConcurrentDictionary, thread-safe]
        → DSMWatcher.Notify(key, value)                 [unchanged]
        → ScheduleSave() debounce                        [unchanged, but race fix applied]
```

### Load with Migration

```
Game code: DSM.UseSlot("slot_name")
    → DSMSlotManager.UseSlot() → DSMSlot.Load()
        → acquire I/O gate                                [NEW — atomic vs. concurrent Set/Save]
        → read bytes from disk, decrypt if .enc
        → parse JSON → envelope JObject { version, data }  [NEW envelope shape]
        → if envelope.version < DSMConfig.SaveVersion:
              DSMMigrationRunner.Migrate(envelope)          [NEW step — stepwise up to current]
        → DSMSchema.ValidateAll(envelope.data)              [NEW — sanity check post-migration]
        → populate _data (ConcurrentDictionary) from envelope.data
        → SeedDefaults() for any keys still missing          [unchanged]
        → release I/O gate
```

### Key Rotation

```
Developer/ops code: DSM.RotateEncryptionKeyAsync(newKey)
    → DSMSlotManager.RotateEncryptionKeyAsync(newKey)
        → for each known slot name (cached + on-disk discovered):
              slot = GetOrCreateSlot(name)
              acquire slot I/O gate                          [reuse thread-safety primitive]
              slot.Load()            // decrypts with DSMConfig.EncryptionKey (old)
              DSMConfig.EncryptionKey = newKey (scoped, not global, during the op)
              slot.Save()            // re-encrypts with new key, fresh salt/IV via existing DSMEncryptor
              release gate
        → commit DSMConfig.EncryptionKey = newKey globally once all slots succeed
        → on any slot failure: abort before committing the global key change (avoid partial-rotation state
          where some slots are encrypted with old key, some with new, and the config no longer matches either)
```

## Build Order

Ordered by hard dependency, not by calendar/phase size. Each item lists **why it must come before the next**.

1. **Thread-safety (`DSMSlot._data`, `DSMSlotManager._slots`, `ScheduleSave` CTS race fix).**
   Must land first. Every other feature reads/writes the exact same `_data` dictionary and the exact same `Load`/`Save` code paths. Building schema validation, migration, or key rotation on top of an unsynchronized dictionary just adds more concurrent writers to an already-racy structure — new features would inherit the bug, not fix it.

2. **Concurrent-access test suite.**
   Write immediately after (1), before any further feature work. This is the direct answer to "does thread-safety need to land before concurrent-access tests are meaningful": yes — a test asserting `Set()` + `Load()` don't corrupt state is meaningless (flaky, or trivially "passes" by luck of thread scheduling) until the underlying primitive is actually synchronized. Landing tests right after thread-safety, before schema/migration/rotation, also locks in the `Load`/`Save`/`Set`/`Get` contract as a regression harness for every subsequent change that touches those same methods.

3. **Schema validation (`DSMSchema`, `DSMDataEntry` type registry wiring, `DSMConfig.StrictSchema`).**
   Depends on (1) — validation wraps `Set`/`Get`, which must already be safe to call concurrently. Independent of migration in principle, but should land before migration because migration's whole purpose is "produce data that satisfies the current schema" — the schema contract needs to exist before there's a target for migrations to migrate *toward*, and before `DSMMigrationRunner` can call it as a post-migration sanity check.

4. **Versioning + migration (`DSMConfig.SaveVersion`, envelope file format, `IDSMMigration`, `DSMMigrationRunner`).**
   Depends on (1) for atomic `Load()`, and on (3) to validate migrated output. This is the biggest on-disk breaking change in the milestone — batch it here rather than spreading file-format changes across multiple later increments, since users are already told "no backward compatibility" once, not twice.

5. **Encryption key rotation (`DSMSlotManager.RotateEncryptionKeyAsync`).**
   Depends on (1) for the atomic load-then-save cycle (a rotation racing a concurrent `Set()`/autosave could produce a slot re-encrypted with a stale in-memory snapshot). Not hard-dependent on (3) or (4), but sequencing it after migration is pragmatic: it reuses the same "read old shape → write current shape, atomically" plumbing that migration just established, so building it right after avoids re-deriving the same atomicity pattern twice.

6. **Editor integration (new version/migration/rotate-key panel; wire into the planned `DSMManagerWindow` decomposition).**
   Depends on all of (1)–(5) existing as runtime APIs to call into. This is also the natural point to execute the already-planned `DSMManagerWindow.cs` split (from `CONCERNS.md`) — new admin UI should be built as new focused classes from day one rather than added to the existing 825-line file and then split out later.

**Cross-cutting note on tests:** because `DSMSlotManager` is a static singleton with an existing `DSM.Configure()` test-injection escape hatch (see `.planning/codebase/ARCHITECTURE.md`, Anti-Patterns), all new components should follow the same testability discipline already established in this codebase — `DSMSchema` and `DSMMigrationRunner` in particular should be pure, Unity-API-free classes operating on `JObject`/`JToken`, so they can be exercised in EditMode tests without a running scene, consistent with the "no test framework yet — introduce one" scope note in `PROJECT.md`.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Locking across an `await`

**What people do:** Wrap `DSMSlot.LoadAsync()`/`SaveAsync()` bodies in a plain `lock (_gate) { ... await ... }`.
**Why it's wrong:** C# does not allow `await` inside `lock`, and even where a workaround compiles (`Monitor.Enter`/`Exit` manually), holding a monitor across an `await` risks the continuation resuming on a different thread and releasing a lock it never held, or blocking the Unity main thread while a background continuation waits — this is exactly the class of bug already present in `ScheduleSave()`'s CTS disposal race (`CONCERNS.md`, Fragile Areas).
**Do this instead:** Use `SemaphoreSlim.WaitAsync()`/`Release()` for any critical section that spans an `await` (Load/Save/Rotate). Reserve plain `lock`/`ConcurrentDictionary` for synchronous, in-memory-only operations (`Set`/`Get`).

### Anti-Pattern 2: Growing `DSMManagerWindow.cs` further

**What people do:** Add "just one more" section to the existing 825-line window for version display, migration status, and a rotate-key button, because the window already has slot-selection UI.
**Why it's wrong:** Directly works against the tech-debt item already logged for this exact file; makes the eventual split harder, not easier, since new logic gets entangled with the reflection/rendering/slot-ops code already mixed together there.
**Do this instead:** New editor UI for hardening features goes in new files that call into the runtime components (`DSMSlotManager`, `DSMSchema`, `DSMMigrationRunner`) — treat this milestone as the first slice of the planned decomposition, not an exception to it.

### Anti-Pattern 3: Key-version header/multi-key registry for a single local encryption key

**What people do:** Copy enterprise key-rotation patterns wholesale (key-version tags per ciphertext, "retired" keys kept around indefinitely, gradual re-encryption in the background).
**Why it's wrong:** DSM has exactly one encryption key per `DSMConfig`, used for local single-player save files — there is no multi-tenant or server-side decrypt-with-any-historical-key requirement. Building a key-version registry adds a second persistent-state concept (which key version encrypted which file) for a problem that a synchronous decrypt-old/re-encrypt-new pass already solves.
**Do this instead:** `RotateEncryptionKeyAsync` performs a direct load-with-old-key → save-with-new-key cycle per slot, and only commits the new key to `DSMConfig` after all slots succeed (fail atomically, not partially).

## Integration Points (Internal Boundaries)

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `DSMSlot` ↔ `DSMSchema` | Direct method call (`Validate`, `ValidateAll`) inside `Set`/`Get`/`Load` | `DSMSchema` has no upstream dependents besides `DSMSlot`; mirrors existing `DSMSlot` ↔ `DSMSerializer` boundary |
| `DSMSlot` ↔ `DSMMigrationRunner` | Direct call from `Load()`, operates on `JObject` envelope only | No file I/O inside the runner; keeps it unit-testable |
| `DSMSlotManager` ↔ `DSMSlot` (rotation) | `DSMSlotManager` orchestrates the per-slot loop; `DSMSlot` still owns the actual Load/Save mechanics | Matches existing pattern: `DSMSlotManager` never does file I/O itself |
| `DSMConfig` ↔ everything | Pure data reads (`SaveVersion`, `StrictSchema`, `EncryptionKey`) | No new logic in `DSMConfig`; stays a data holder |
| `Editor` ↔ Runtime hardening components | New editor classes call `DSMSlotManager`/`DSMSchema`/`DSMMigrationRunner` public methods | No duplicated validation/migration/rotation logic in `Editor/` |
| `DSMWatcher` ↔ everything else | Unchanged | Schema/version/rotation are orthogonal to reactive notification; do not couple them |

## Sources

- Direct codebase read (HIGH confidence): `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/STRUCTURE.md`, `.planning/codebase/CONCERNS.md`, `.planning/PROJECT.md` — all read in full for this research.
- Web validation (LOW confidence — general community/blog consensus, not authoritative single-source, used only to corroborate the internal design already implied by the codebase, not as the basis for it):
  - [A No-Nonsense C# Async, Threading & Concurrency Guide](https://dev.to/recurpixel/a-no-nonsense-c-async-threading-concurrency-guide-36em) — `ConcurrentDictionary` over `ReaderWriterLockSlim` for async-touched shared state; `ReaderWriterLockSlim` is not await-safe.
  - [Effective Concurrency Control in C# Using ReaderWriterLockSlim](https://en.ittrip.xyz/c-sharp/csharp-readerwriterlockslim) — read/write lock trade-offs.
  - [A Practical Save System for Indie Games: Versioned, Portable, Testable](https://arcadeonstudios.co.uk/blog/a-practical-save-system-for-indie-games-versioned-portable-testable) — versioned envelope + stepwise migration + atomic writes pattern (directly informed the envelope/migration-runner design above).
  - [How to Create Encryption Key Rotation](https://oneuptime.com/blog/post/2026-01-30-encryption-key-rotation/view) — key-version-header pattern (used here as a contrast case — explicitly *not* adopted, see Anti-Pattern 3, since it is scoped for multi-tenant/server systems, not a single local key).

---
*Architecture research for: DataSaveManager (DSM) hardening milestone*
*Researched: 2026-07-08*
