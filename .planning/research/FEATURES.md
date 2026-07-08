# Feature Research

**Domain:** Unity game save-system hardening (encryption key rotation, schema versioning/migration, thread-safe concurrency, reactive-change batching, type-safe schema validation)
**Researched:** 2026-07-08
**Confidence:** MEDIUM (web-sourced, cross-checked across multiple independent sources per topic; no single authoritative spec exists for "save system best practices" the way there is for, say, a language spec)

## Feature Landscape

### Table Stakes (Users Expect These)

These are the four items already named in `PROJECT.md` "Missing Critical Features" plus one adjacent item (atomic writes) that every mature save library treats as a prerequisite for the other four. A "production-hardened" save system that ships schema validation, versioning, key rotation, and batching but still writes saves non-atomically is still fragile — corruption on crash-during-write undermines all the other guarantees.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Type-safe schema validation on Set/Get** | Without it, `Set<int>("Gold", ...)` followed by `Set<string>("Gold", ...)` silently corrupts state and later `Get<int>("Gold")` fails or returns default with no diagnostic. Every mature typed store (Unity's `PlayerPrefs` is the cautionary counter-example — it has none, and it's infamous for silent type-coercion bugs) treats this as baseline. DSM already has the type registry it needs via `DSMConstant` reflection (key -> expected type is knowable at codegen time). | MEDIUM | Enforce on `Set()`: reject/throw if the runtime type doesn't match the type registered for that key. Enforce on `Get<T>()`: throw a clear, typed exception (not a silent default) on mismatch, rather than letting `JToken.ToObject<T>()` throw an opaque Newtonsoft exception deep in the stack. |
| **Save schema/file versioning** | Any save format that will change over the product's lifetime needs a version marker or every future schema change becomes a breaking change for existing save files. This is the single most common regret cited in "I wish I'd added this from day one" game-dev postmortems. | LOW–MEDIUM | Add an integer `SaveVersion` (or similar) to the slot's file envelope, separate from the payload. This is a prerequisite for migration (below) — version must exist before there's anything to branch migration logic on. |
| **Migration on load** | Once a version field exists, users expect old saves to keep working after an update, not to silently reset or throw. Migration is the mechanism that fulfills that expectation. Industry-standard pattern: a version number is stored per record/file, and at load time an adapter chain steps the data forward one version at a time (v1→v2→v3…) until it reaches the current shape, then deserializes into the live model. | MEDIUM | Requires versioning to exist first (dependency, see below). Two sub-patterns exist — **lazy migration** (migrate a save only when it's loaded, write the migrated result back immediately) and **bulk migration** (migrate every save at startup). Lazy is the better fit for DSM: it's a slot-based, mostly-one-active-slot system, so migrating on-demand avoids paying the cost for slots the player never touches again. Migration should be expressed as a chain of small `IDSMMigration` steps registered per version transition, not one giant if/else — this keeps each step testable in isolation and matches how schema-registry tooling (Avro/Protobuf-style "expand-switch-contract") structures the same problem outside games. |
| **Thread-safe concurrent access to slot data** | `DSMSlot._data` is currently unsynchronized and accessed from `Set()`/`Get()`/`Load()`/`Save()`, some of which run through async/UniTask paths. Any save system that supports async save/load (DSM already does) must guarantee the in-memory store can't be corrupted or throw `KeyNotFoundException`/`InvalidOperationException` from a torn enumeration when a background save and a foreground `Set()` interleave. `ConcurrentDictionary<string, JToken>` is thread-safe for individual get/set with lock-free reads and per-bucket write locks — no external lock needed for those. For the *serialize-a-consistent-snapshot* step of `Save()`/`SaveAsync()`, a single lock (or `ReaderWriterLockSlim`, which measurably outperforms plain `lock`/Monitor in read-heavy/write-rare workloads — one benchmark showed ~371ms vs ~981ms) around "copy the current state, then serialize the copy" is still needed, because per-key thread-safety doesn't guarantee a *point-in-time-consistent* snapshot across all keys. | LOW–MEDIUM | Migrate `_data` to `ConcurrentDictionary`, and wrap the snapshot-for-save step (not every individual access) in a lock. Also close the debounce-timer disposal race noted in CONCERNS.md (`ScheduleSave()`), since a race there defeats thread-safety gains elsewhere. |
| **Atomic file writes (write-temp-then-rename)** | Not explicitly named in CONCERNS.md but implied by "no silent corruption under concurrent access" in the Core Value. The standard, near-universal pattern for save-file corruption prevention: serialize to a `.tmp` file, flush/fsync, then rename over the real save file. Rename is atomic on POSIX and effectively atomic in practice on the platforms Unity targets (there are OS-specific caveats on Windows, but write-temp-then-rename is still strictly safer than write-in-place). A crash mid-write leaves the previous good save intact instead of a half-written, unparseable file. | LOW | This is cheap to add and de-risks every other feature here — versioning/migration and key rotation both involve rewriting the save file, and both become genuinely dangerous operations without atomic writes underneath them. |

### Differentiators (Competitive Advantage)

Not required for the hardening pass to be "complete," but they're the difference between "we patched the known gaps" and "this is now a save system other Unity devs would trust with their production title." These align with the Core Value (correct and safe save/load) without over-building.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Key-version header for encryption rotation (not full envelope encryption)** | Store a small `KeyVersion` marker alongside each encrypted slot file. On load, decrypt using the key registered for that version; on explicit "rotate" call, decrypt with old key, re-encrypt with new key, bump the marker. This gives real key-rotation capability (the thing CONCERNS.md flags as entirely missing) without the complexity of cloud-style envelope encryption (data-key/key-encryption-key layering), which is overkill for a local single-player save file. | MEDIUM | Depends on encryption key *validation* already being enforced (an Active bug-fix item in PROJECT.md) — rotation logic built on top of an unvalidated/possibly-empty key is building on sand. Also depends on atomic writes, since re-encryption is a full-file rewrite. |
| **`HasEncryptionKey` / key-state introspection API** | Lets calling code (and the Editor window) check key state programmatically instead of hitting a silent failure. Directly closes the "Encryption with Empty Key" security concern from CONCERNS.md. | LOW | Simple property on `DSMConfig`. Natural companion to key rotation — rotation logic needs to know "is there currently a valid key" anyway. |
| **Debounced/per-frame-batched watcher notifications** | `WatchAsync<T>()` currently notifies synchronously on every `Set()`. Under high-frequency changes (e.g., a counter incrementing every frame — relevant given this package lives in `counter-stack-game`) this causes frame drops. The standard Rx pattern here is `Buffer`/`Sample`/`Throttle` — collect changes over a window (either a time window or "once per frame") and flush once. UniRx exposes exactly this via frame-based operators (`ThrottleFrame`, `SampleFrame`, `EveryUpdate`) that run on the main-thread scheduler, which is the right mental model even though DSM doesn't depend on UniRx today (UniTask is the existing async dependency). | MEDIUM | Implementation: queue `(key, value)` change events, flush the queue once per frame (or after a short debounce) instead of notifying each subscriber synchronously inside `Set()`. Must coexist with the thread-safety work above — the queue itself needs to be safe for a background save thread to enqueue into while the main thread drains it. |
| **Migration dry-run / pre-migration backup** | Before running a migration chain against a save file, copy the original file aside (e.g., `slot.bak`) so a buggy migration step can't destroy the only copy of a player's save. | LOW–MEDIUM | Pure insurance policy on top of the migration feature; not required for migration to "work," but it's the difference between a migration bug being a minor annoyance vs. a support fire. |
| **LRU eviction / manual unload for the in-memory slot cache** | CONCERNS.md flags unbounded slot cache growth as a scaling limit. Not urgent for typical single/few-slot usage, but a natural companion once thread-safety work touches `DSMSlotManager` anyway. | LOW–MEDIUM | Defer unless a concrete use case (e.g., per-user-id slots) emerges — see anti-features below for why *not* to build this speculatively right now. |

### Anti-Features (Commonly Requested, Often Problematic)

Things that sound like "proper hardening" but are disproportionate for a solo/small-team Unity save package with local, single-player, `persistentDataPath`-only storage (per PROJECT.md's explicit Out of Scope).

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| **Full envelope encryption (separate DEK wrapped by a KEK, key hierarchy, HSM-style key management)** | This is the "correct" cloud/enterprise pattern for key rotation (Google KMS, AWS KMS all use it), and it's tempting to copy it because it's the textbook answer. | For a local save file with one key in play at a time, a data-key layer adds a second encrypted artifact, a second failure mode, and no real security benefit — the "master key" and "data key" would both live on the same untrusted client device anyway. It solves a *multi-tenant, server-side* problem DSM doesn't have. | The key-version-header pattern in Differentiators above gets 90% of the practical benefit (auditable rotation, no re-encrypt-everything-at-once requirement) at a fraction of the complexity. |
| **Generic schema-registry / backward-and-forward-compatible schema evolution (Avro/Protobuf-style field-tagging, wire-compatible schema diffing)** | Feels like the "enterprise-grade" way to do versioning, especially since it's the term that shows up first in general schema-versioning research. | DSM's data model is a flat key→JToken map defined by a single `DSMConstant` class per project, not a distributed system with independent producers/consumers that need to interoperate across versions simultaneously. A registry/compatibility-checker is solving a problem (multiple services agreeing on a wire format in real time) that doesn't exist here. | The lighter "expand-switch-contract" idea *does* transfer usefully in a narrow way: when renaming/removing a `DSMConstant` key, keep reading the old key as a fallback for one version before removing it, rather than needing a full schema registry. |
| **Bulk-migrate every slot on app startup** | Feels "safer" — get everyone current immediately. | Most players have 1–3 active slots; eagerly touching every save file on disk (including ones the player may never load again) adds startup cost and risk for no benefit, and PROJECT.md already accepts breaking old-save compatibility isn't a hard requirement here. | Lazy migration (migrate on load, as specified in Table Stakes) — pay the cost only for slots actually used. |
| **Distributed/multi-process file locking (mutexes, OS-level file locks for cross-process safety)** | "Thread-safe" research naturally surfaces distributed-systems-flavored locking patterns. | DSM's concurrency problem is *in-process* (main thread vs. UniTask background save task), not multiple OS processes writing the same file. Cross-process locking is solving a problem — two separate Unity player instances/editor+player writing the same save simultaneously — that isn't in this package's stated use case (multiplayer/networked save sync is explicitly Out of Scope in PROJECT.md). | `ConcurrentDictionary` + a snapshot lock (Table Stakes) is sufficient for the actual threading model in play. |
| **Backward compatibility with pre-v1.0 save files and encryption format** | Sounds responsible on its face. | PROJECT.md explicitly confirms this is Out of Scope for this milestone — breaking changes are accepted to fix validation/security correctly. Building compatibility shims for the old fixed-salt encryption format (already documented as broken/insecure) would mean carrying forward the exact insecure behavior this milestone exists to remove. | Ship the hardened format as a clean break; document it in the changelog/README as a breaking change, as CONCERNS.md's "Breaking Change Migration Not Tested" section already anticipates. |
| **Speculative LRU slot-cache eviction ahead of any real memory-pressure symptom** | Adjacent to the thread-safety and slot-manager work, so it's tempting to bundle in now. | No evidence in CONCERNS.md or PROJECT.md that any consumer currently creates enough slots for this to matter; building an eviction policy without a concrete access pattern to tune it against risks solving the wrong problem (e.g., evicting a slot the game logic assumed stays resident). | Keep as a Differentiator/backlog item; revisit if/when a real project reports memory growth from many slots. |

## Feature Dependencies

```
Encryption key validation (bug-fix, already Active in PROJECT.md)
    └──requires──> Encryption key rotation
                       └──requires──> Atomic file writes (rotation = full-file rewrite)

Save schema versioning (version field in file envelope)
    └──requires──> Migration on load (nothing to branch on without a version)
                       └──requires──> Atomic file writes (migration = full-file rewrite)
                       └──enhances──> Migration dry-run / backup (optional safety net)

Type-safe schema validation on Set/Get
    ──independent of──> versioning/migration (validation guards live data; migration transforms stored data)
    ──enhances──> Migration (a typed schema makes it possible to validate migration output, not just input)

Thread-safety (ConcurrentDictionary + snapshot lock)
    ──requires-before──> Batched/deferred watcher notifications (the notification queue itself must be thread-safe,
                          since Set() can be called from the same threads that trigger notifications)
    ──requires-before──> Encryption key rotation and Migration (both read-then-rewrite the whole slot;
                          doing that safely needs the same data to not be mutated mid-operation)

Atomic file writes
    ──enhances──> everything that rewrites a save file (rotation, migration) — foundational, do first among the "rewrite" features

LRU slot-cache eviction (deferred)
    ──conflicts with──> naive thread-safety assumptions if added later without care
                          (eviction must not race with an in-flight SaveAsync on the slot being evicted)
```

### Dependency Notes

- **Versioning must land before migration**, and migration logic should assume atomic writes are already in place — writing a half-migrated file on crash is worse than the pre-migration bug, because it corrupts data the old code could still have read.
- **Thread-safety is a prerequisite, not a peer, for key rotation and migration.** Both operations are "read the whole slot, transform it, write the whole slot back" — if `_data` isn't already safe against concurrent `Set()` calls during that read/transform/write, rotation and migration inherit that race instead of fixing it.
- **Type-safe validation is independent** of the versioning/migration/rotation chain — it guards live in-memory access, not the file format — so it can be built and shipped in parallel with (or even before) the others without blocking them.
- **Batched watcher notifications depend on thread-safety** because the notification queue is written to from whatever thread calls `Set()` (potentially a background save-completion callback) and drained on the main thread/frame loop — that hand-off needs the same synchronization discipline as the data store itself.
- **Atomic writes is the one foundational item not named in CONCERNS.md's "Missing Critical Features"** but that both key rotation and migration silently assume; recommend surfacing it as an explicit requirement rather than an implementation detail buried inside those two features.

## MVP Definition

Reframed for a hardening milestone (not a new product): "Launch" = the milestone is not honestly "hardened" without it; "Add after" = strengthens the hardening but the milestone still delivers real value without it; "Future" = out of scope for this milestone by design.

### Launch With (this milestone)

- [ ] Type-safe schema validation on `Set`/`Get` — the most direct fix for the "silent type mismatch" failure mode named in CONCERNS.md
- [ ] `SaveVersion` field + migration-on-load mechanism — without this, every future DSM schema change repeats today's problem
- [ ] Thread-safe `DSMSlot._data` (ConcurrentDictionary + snapshot lock) + fixed debounce-timer race — prerequisite for rotation/migration to be safe, and independently closes a named "Fragile Area"
- [ ] Encryption key rotation via key-version header (not full envelope encryption) — directly closes "No Encryption Key Rotation" from CONCERNS.md
- [ ] Batched/deferred watcher notifications (per-frame flush) — directly closes "No Async Watchers" batching gap from CONCERNS.md
- [ ] Atomic write-temp-then-rename for slot saves — foundational safety net underneath rotation and migration; cheap, should not be skipped

### Add After Validation (v1.x, once core hardening ships)

- [ ] Migration dry-run/backup-before-migrate — add once the migration mechanism itself is proven in real use
- [ ] `HasEncryptionKey` introspection API — small ergonomic addition, not blocking
- [ ] Expand-switch-contract style key-rename fallback (read old key for one version before removing) — add opportunistically when the first real key rename happens

### Future Consideration (v2+, explicitly deferred)

- [ ] LRU/manual eviction for the in-memory slot cache — defer until a real project shows memory pressure from many slots
- [ ] Cross-process file locking — out of scope; DSM's concurrency model is in-process only
- [ ] Full envelope encryption / key hierarchy — deliberately rejected as anti-feature, not merely deferred

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Thread-safe `DSMSlot._data` | HIGH | LOW–MEDIUM | P1 |
| Atomic file writes | HIGH | LOW | P1 |
| Save versioning + migration-on-load | HIGH | MEDIUM | P1 |
| Type-safe Set/Get validation | HIGH | MEDIUM | P1 |
| Encryption key rotation (key-version header) | MEDIUM–HIGH | MEDIUM | P1 |
| Batched watcher notifications | MEDIUM | MEDIUM | P1 |
| Migration dry-run/backup | MEDIUM | LOW–MEDIUM | P2 |
| `HasEncryptionKey` introspection | LOW | LOW | P2 |
| Key-rename fallback (expand-switch-contract) | LOW | LOW | P3 |
| LRU slot-cache eviction | LOW (no observed need) | MEDIUM | P3 |
| Full envelope encryption | LOW (over-engineered for use case) | HIGH | Rejected |
| Cross-process file locking | NONE (out of scope) | HIGH | Rejected |

**Priority key:**
- P1: Must have — this milestone is not "hardened" without it
- P2: Should have, add when the P1 mechanism it builds on is proven
- P3: Nice to have, revisit only if a concrete need appears
- Rejected: Anti-feature for this project's scope, do not build

## Competitor / Reference Implementation Analysis

| Feature | Easy Save 3 (Unity, commercial, mature) | Cloud KMS pattern (AWS/GCP, enterprise reference) | DSM's Approach |
|---------|------------------------------------------|----------------------------------------------------|-----------------|
| Key rotation | Not supported for existing files — documented guidance is to wipe/clear persistent data path rather than migrate when the password changes. This is a known, accepted gap in a widely-used commercial asset. | Envelope encryption: rotate the KEK, only re-wrap the small DEK, never touch the bulk data. | Middle ground: key-version header + explicit re-encrypt-on-rotate for the whole (small, local) slot file. Better than ES3's "just wipe it," lighter than full envelope encryption — appropriate for local single-player saves. |
| Versioning/migration | Left to the developer; ES3 provides serialization but no built-in schema-version/migration chain. | N/A (not this domain) | DSM should build this in as a first-class feature — a differentiator vs. ES3, and directly requested by CONCERNS.md. |
| Type safety | ES3 is loosely typed similarly to DSM today (generic `Save<T>`/`Load<T>` with no schema enforcement). | N/A | DSM already has the ingredient (compile-time-known `DSMConstant` types) that ES3-style generic stores don't — validating against that registry is a genuine differentiator, not just parity. |
| Reactive batching | ES3 has no built-in reactive/watch API at all. | N/A | DSM's `WatchAsync<T>` already exceeds ES3 here; batching closes the remaining gap (frame-drop risk under high-frequency `Set()`). |

## Sources

- [How to secure save data? - Unity Discussions](https://discussions.unity.com/t/how-to-secure-save-data/883690)
- [Encryption & Compression - Easy Save for Unity](https://docs.moodkie.com/easy-save-3/es3-guides/es3-encryption-compression/)
- [ES3Settings.encryptionPassword - Easy Save for Unity](https://docs.moodkie.com/easy-save-3/es3-api/es3-properties/es3settings-encryptionpassword/)
- [Encrypting Game Data with Unity - Digital Ephemera](https://videlais.com/2021/02/28/encrypting-game-data-with-unity/)
- [How to prevent external changes to your save files in Unity](https://giannisakritidis.com/blog/Using-Encryption-In-Save-Files/)
- [Data Versioning and Schema Evolution Patterns](https://bool.dev/blog/detail/data-versioning-patterns)
- [Best Practices for Evolving Schemas in Schema Registry](https://docs.solace.com/Schema-Registry/schema-registry-best-practices.htm)
- [What is a good way to implement Saving System with implementation updates? - GameDev.tv](https://community.gamedev.tv/t/what-is-a-good-way-to-implement-saving-system-with-implementation-updates/223573)
- [ConcurrentDictionary<TKey,TValue> Class - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-10.0)
- [Making ConcurrentDictionary GetOrAdd thread safe using Lazy](https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/)
- [When to Use ReaderWriterLockSlim Over lock in C#](https://code-maze.com/csharp-when-to-use-readerwriterlockslim-over-lock/)
- [System.Threading.ReaderWriterLockSlim class - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-threading-readerwriterlockslim)
- [UniRx - Reactive Extensions for Unity (GitHub)](https://github.com/neuecc/UniRx)
- [Reactive programming in Unity with UniRx](https://www.loekvandenouweland.com/content/reactive-programming-in-unity3d-with-unirx.html)
- [buffer | Learn RxJS](https://www.learnrxjs.io/learn-rxjs/operators/transformation/buffer)
- [ReactiveX - Buffer operator](https://reactivex.io/documentation/operators/buffer.html)
- [Save to temporary file and rename to prevent file corruption - IfcOpenShell issue #4797](https://github.com/IfcOpenShell/IfcOpenShell/issues/4797)
- [File Save Operation Should Be Atomic to Prevent Data Loss and Corruption - fritzing issue #4148](https://github.com/fritzing/fritzing-app/issues/4148)
- [How to Test Game Saves for Corruption and Version Incompatibility - Bugnet Blog](https://bugnet.io/blog/how-to-test-game-saves-for-corruption)
- [Envelope encryption - Google Cloud KMS Documentation](https://docs.cloud.google.com/kms/docs/envelope-encryption)
- [Key Rotation Strategies - Replacing Cryptographic Keys Without Downtime](https://www.qcecuring.com/education/key-management/key-rotation-strategies)
- [Key Rotation in KMS: What Really Happens to Your Encrypted Data?](https://medium.com/@madhurajayashanka/key-rotation-in-aws-and-gcp-kms-what-really-happens-to-your-encrypted-data-7d2a12b07303)
- Internal: `.planning/PROJECT.md`, `.planning/codebase/CONCERNS.md` (DSM-specific gaps and constraints)

---
*Feature research for: Unity game save-system hardening (DataSaveManager / DSM)*
*Researched: 2026-07-08*
