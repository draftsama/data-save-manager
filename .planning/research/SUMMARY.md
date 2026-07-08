# Project Research Summary

**Project:** DataSaveManager (DSM) — Unity save-system hardening milestone
**Domain:** Unity local save-system hardening (encryption, versioning/migration, concurrency, schema validation, editor tooling)
**Researched:** 2026-07-08
**Confidence:** MEDIUM-HIGH

## Executive Summary

DSM is not a greenfield build — it's an existing, working layered save system (`DSM` -> `DSMSlotManager` -> `DSMSlot` -> `DSMSerializer`/`DSMEncryptor`/`DSMWatcher`) that this milestone hardens against five known gaps already documented in `CONCERNS.md`: unvalidated/empty encryption keys, no key rotation, unsynchronized concurrent access to slot data, no schema/type validation, and no save-format versioning or migration. Every recommendation stays inside the current stack (Unity 6000+, C# 11, UniTask, Newtonsoft.Json 3.2.1, .NET BCL crypto) — no new runtime packages are needed. The one new first-party addition is Unity's bundled Test Framework (NUnit), since DSM currently has zero automated tests and this milestone is exactly the kind of correctness-critical work that most benefits from test-first development.

The recommended approach: build authenticated encryption from AES-CBC + HMAC-SHA256 (Encrypt-then-MAC) rather than AES-GCM, because GCM is unsupported on WebGL and inconsistently supported on IL2CPP iOS/tvOS — a hard platform constraint, not a preference. Thread-safety must be fixed first and treated as one unit of work spanning both `DSMSlot._data` and the `ScheduleSave()` debounce-timer CTS race (both stem from the same "unsynchronized shared state under async re-entrancy" root cause) — everything else (schema validation, migration, key rotation) reads/writes through those same code paths and will inherit any remaining race if this isn't done properly first. Schema validation should reuse the type metadata DSM's codegen already produces (`DSMConstant`/`DSMDataEntry`), not add a new source of truth. Versioning wraps the save payload in an envelope (`{version, data}`) with a chain of small, composable `IDSMMigration` steps — not one big all-or-nothing rebuild function, which is the most common design mistake made this early in a save system's life. Key rotation should be a straightforward decrypt-old/re-encrypt-new pass (not enterprise-style envelope encryption with a key hierarchy) but must be atomic (temp-file + verify + replace) since a non-atomic rotation can permanently destroy a save if interrupted mid-operation.

The dominant risk pattern across all four research files is **partial fixes that look complete but aren't**: fixing `_data` thread-safety but leaving the CTS race; validating key length but not entropy (and calling it "secure"); adding migration that silently substitutes defaults for renamed keys instead of remapping them; writing concurrency tests that only assert "no exception thrown" instead of asserting final-state correctness; and bundling the planned `DSMManagerWindow.cs` decomposition together with unrelated bug fixes in the same commits, making regressions unattributable. Mitigation is procedural as much as technical: sequence thread-safety before everything else, write regression tests test-first (red before green) against the bugs already enumerated in `CONCERNS.md`, keep structural refactors and behavior fixes in separate commits, and build a `TestFixtures/` directory of versioned save files so migration doesn't silently rot.

## Key Findings

### Recommended Stack

No new runtime dependencies. Hardening is built entirely from BCL primitives already referenced in the codebase (`Aes`, `Rfc2898DeriveBytes`) plus one new addition — `HMACSHA256` for authenticated encryption — and Unity's bundled `com.unity.test-framework` (NUnit) for the first test suite this package has ever had. Three candidate libraries were explicitly evaluated and rejected: `Newtonsoft.Json.Schema` (commercial/rate-limited, unnecessary for DSM's narrow validation surface), `ConcurrentDictionary` (anecdotal IL2CPP enumeration issues; a plain `Dictionary` + `lock`/snapshot pattern is safer for DSM's coarse-grained access pattern), and mocking frameworks like Moq (dynamic-proxy codegen is fragile under IL2CPP AOT).

**Core technologies:**
- `Aes` (CBC) + `HMACSHA256` (Encrypt-then-MAC): authenticated, fully-managed encryption — works on every Unity platform including WebGL, unlike `AesGcm` which is permanently excluded on browser and unreliable on IL2CPP iOS/tvOS.
- `Rfc2898DeriveBytes` (existing instance API, not the newer static `.Pbkdf2()` helper): PBKDF2 key derivation, already proven working in this codebase's IL2CPP builds.
- NUnit via `com.unity.test-framework`: Unity's only first-party test runner, bundled and version-locked to the Editor — no viable alternative in the ecosystem.

### Expected Features

**Must have (table stakes) — this milestone is not "hardened" without these:**
- Type-safe schema validation on `Set`/`Get` (reject/throw on type mismatch instead of `PlayerPrefs`-style silent corruption)
- `SaveVersion` field + migration-on-load (lazy migration, per-slot, not bulk-at-startup)
- Thread-safe `_data` + fixed debounce/CTS race (one unit of work, not two)
- Encryption key rotation via direct decrypt-old/re-encrypt-new rotation (not full envelope encryption)
- Batched/deferred watcher notifications (per-frame flush, not synchronous-per-`Set()`)
- Atomic write-temp-then-rename for slot saves — the foundational item not named explicitly in CONCERNS.md but silently required by both rotation and migration

**Should have (differentiators):**
- `HasEncryptionKey` introspection API
- Migration dry-run / pre-migration backup (`.bak` before migrating)
- Expand-switch-contract style key-rename fallback for renamed `DSMConstant` keys

**Defer (v2+, explicitly out of scope):**
- Full envelope encryption / key hierarchy (solves a multi-tenant/server problem DSM doesn't have)
- LRU/manual slot-cache eviction (no observed memory-pressure need yet)
- Cross-process file locking (DSM's concurrency model is in-process only)
- Backward compatibility with pre-v1.0 saves/encryption format (explicitly accepted as a breaking change)

### Architecture Approach

All four hardening capabilities slot additively into the existing layer boundaries — no re-layering. Two new pure, Unity-API-free, unit-testable components are added: `DSMSchema` (key->type validation, built once per slot from existing codegen metadata) and `DSMMigrationRunner` + `IDSMMigration` (stepwise `JObject` transforms between versions, called from `DSMSlot.Load()` between deserialize and `_data` population). `DSMSlot` and `DSMSlotManager` both gain thread-safe collections and an I/O gate (`SemaphoreSlim`, not plain `lock`, since Load/Save/Rotate span `await` boundaries). Key rotation is orchestration, not a new persistence primitive — `DSMSlotManager.RotateEncryptionKeyAsync` decrypts-with-old/re-encrypts-with-new per slot and only commits the new key to `DSMConfig` after every slot succeeds. The public API surface changes are additive-only (`DSM.Set<T>`/`Get<T>` signatures unchanged; new `RotateEncryptionKeyAsync`, `SaveVersion`, `StrictSchema`).

**Major components:**
1. `DSMSchema` (NEW) — key->expected-type validation, consumed by `Set`/`Get`/post-migration sanity check
2. `IDSMMigration` / `DSMMigrationRunner` (NEW) — pure `JObject` transforms, chained v1->v2->v3, no file I/O
3. `DSMSlot` (CHANGED) — thread-safe `_data`, I/O gate for atomic Load/Save/Rotate, envelope read/write
4. `DSMSlotManager` (CHANGED) — thread-safe slot cache, orchestrates rotation across slots
5. Editor companion classes (NEW, separate files) — version/migration/rotate-key panel, explicitly not appended to the already-flagged 825-line `DSMManagerWindow.cs`

### Critical Pitfalls

1. **Fixing `_data` but leaving the `ScheduleSave()` CTS/debounce race unsynchronized** — both are the same root cause (unsynchronized shared state under async re-entrancy); fix in the same phase, not two.
2. **Non-atomic key rotation (reuse `Save()` directly)** — an interruption mid-rotation can destroy a save unrecoverably with either key. Always temp-file write -> verify round-trip -> atomic replace; never discard the old key from memory until the new file is confirmed durable.
3. **Single global `SaveVersion` int implemented as one monolithic full-slot-rebuild function** — works for migration #1, balloons in complexity by migration #2-3. Design the migration contract as small composable per-step transforms from day one.
4. **Migration silently substitutes defaults for renamed keys instead of remapping them** — looks like success (no exception) but is silent data loss. Migration authors must explicitly enumerate renamed/removed keys.
5. **"No exception thrown" concurrency tests that don't catch lost writes** — assert final data-state correctness, not just absence of a crash; races often manifest as silent last-write-wins, not exceptions.

## Implications for Roadmap

Based on research, suggested phase structure (aligned with the Build Order derived in ARCHITECTURE.md and the phase-mapping in PITFALLS.md):

### Phase 1: Thread-Safety Foundation + Test Infrastructure
**Rationale:** Every other feature reads/writes the exact same `_data` dictionary and `Load`/`Save` code paths. Building anything else first means new features inherit existing races instead of fixing them. This is also the natural point to stand up the test framework (asmdefs, fixtures directory) since concurrency tests are meaningless until the primitive they test is actually synchronized.
**Delivers:** Thread-safe `DSMSlot._data`/`DSMSlotManager._slots`, fixed `ScheduleSave()` CTS race, `Tests/Editor` + `Tests/Runtime` asmdefs, first concurrency regression tests (asserting final-state correctness, not just "no exception").
**Addresses:** Thread-safe concurrent access (Table Stakes)
**Avoids:** Pitfall 4 (partial thread-safety fix), Pitfall 5 (misdiagnosing async re-entrancy as OS threading), Pitfall 6 (false-positive concurrency tests), Pitfall 12 (tests written after fixes)

### Phase 2: Encryption Hardening — Key Validation + Rotation
**Rationale:** Independent of schema/versioning work; directly closes the two Active bug-fix items already flagged in `CONCERNS.md` (empty-key acceptance, no rotation). Sequenced early because key rotation reuses the atomic load-then-save cycle that also benefits migration later — establishing the atomicity pattern once here avoids re-deriving it twice.
**Delivers:** Centralized key validation (single `GetValidatedEncryptionKey()`/`HasEncryptionKey` accessor used by both runtime and Editor), AES-CBC + HMAC-SHA256 Encrypt-then-MAC, atomic write-temp-then-rename primitive, `DSM.RotateEncryptionKeyAsync(newKey)`.
**Uses:** `Aes`, `HMACSHA256`, `Rfc2898DeriveBytes` (STACK.md)
**Implements:** Atomic file writes + key rotation orchestration (ARCHITECTURE.md)
**Avoids:** Pitfall 1 (inconsistent key validation across call sites), Pitfall 2 (length-only validation mistaken for strength), Pitfall 3 (non-atomic rotation)

### Phase 3: Schema Validation
**Rationale:** Depends on Phase 1 (validation wraps `Set`/`Get`, which must already be concurrency-safe). Independent of versioning in principle but should land before migration since migration's target shape needs a schema to migrate toward.
**Delivers:** `DSMSchema` (pure, Unity-API-free, built from existing `DSMDataEntry` codegen metadata), `DSMConfig.StrictSchema` flag (lenient default to avoid breaking existing call sites).
**Addresses:** Type-safe schema validation (Table Stakes)

### Phase 4: Save Versioning + Migration
**Rationale:** The biggest on-disk breaking change in the milestone — batch it into one increment since "no backward compatibility" is already accepted once. Depends on Phase 1 (atomic Load) and Phase 3 (validate migrated output against the schema).
**Delivers:** Envelope file format (`{version, data}`), `IDSMMigration` interface, `DSMMigrationRunner` (composable per-step chain, not full-slot rebuild), `TestFixtures/` directory of versioned sample saves with regression tests.
**Addresses:** Save schema/file versioning, migration on load (Table Stakes)
**Avoids:** Pitfall 7 (global version can't express partial migrations), Pitfall 8 (migration rots with no regression suite), Pitfall 9 (silent default-substitution instead of remap)

### Phase 5: Batched Watcher Notifications
**Rationale:** Orthogonal to versioning/migration/rotation — can be developed in parallel with Phase 3/4 if desired, but depends on Phase 1's thread-safety since the notification queue is written from whatever thread calls `Set()` and drained on the main thread.
**Delivers:** Per-frame-batched `WatchAsync<T>` notification flush instead of synchronous-per-`Set()`.
**Addresses:** Debounced/per-frame-batched watcher notifications (Differentiator)

### Phase 6: Editor Tooling Integration + `DSMManagerWindow` Decomposition
**Rationale:** Depends on all runtime APIs from Phases 1-5 existing to call into. This is the natural point to execute the already-planned decomposition of the 825-line `DSMManagerWindow.cs` — new admin UI (version/migration/rotate-key panel) should be built as new focused classes from day one, not appended then split later.
**Delivers:** New Editor classes for slot version display, migration status, rotate-key action, schema validation errors — all calling into `DSMSlotManager`/`DSMSchema`/`DSMMigrationRunner`, no duplicated logic in `Editor/`.
**Avoids:** Pitfall 10 (IMGUI control-order breaks during split), Pitfall 11 (refactor and bug-fix bundled in the same commits — sequence structural-only commits separately from behavior fixes)

### Phase Ordering Rationale

- Thread-safety is a hard prerequisite for every other phase — it's the one dependency every research file (STACK, FEATURES, ARCHITECTURE, PITFALLS) independently converges on as "must come first."
- Key rotation is sequenced early (Phase 2) rather than last because it establishes the atomic load-then-save pattern that migration (Phase 4) reuses — doing it twice independently risks two different (possibly inconsistent) atomicity implementations.
- Schema validation (Phase 3) is placed before migration (Phase 4) because migration needs a validation target to migrate toward and to sanity-check its own output against.
- Editor tooling is deliberately last: it's pure consumption of runtime APIs, and bundling it with any of Phases 1-5 risks exactly the "refactor + bug-fix in the same commit" pitfall the research flags as unrecoverable-to-bisect.
- Test-first discipline (write the failing test against current buggy behavior, then implement the fix) should be a Definition-of-Done requirement inside every phase above, not deferred to a separate later "testing phase" — this is directly warned against in Pitfall 12.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Encryption Hardening):** Needs a `--research-phase` pass on PBKDF2 iteration counts for the actual minimum-spec target device (OWASP's 600k-iteration guidance needs local benchmarking; STACK.md flags this as unverified against live OWASP docs).
- **Phase 4 (Versioning + Migration):** Needs a design pass before implementation — the migration interface shape (per-step composable transforms vs. full rebuild) is a decision with long-term consequences (Pitfall 7) and isn't fully pinned down by research; work through it explicitly during `/gsd-discuss-phase`.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Thread-Safety + Test Infra):** Well-documented Unity/C# patterns — `SemaphoreSlim` for async-spanning critical sections, `[UnityTest]`/`async Task` for tests. Established, not novel.
- **Phase 3 (Schema Validation):** Straightforward — reuses existing codegen metadata, no external pattern research needed.
- **Phase 5 (Batched Watchers):** Standard debounce/buffer pattern, well understood.
- **Phase 6 (Editor Tooling):** IMGUI ordering pitfall is well-documented (official Unity sources); execution is mechanical once Phases 1-5 exist.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM-HIGH | Platform-support claims (AesGcm WebGL exclusion) verified directly against official Microsoft API docs. Exact bundled Test Framework version for Unity 6000.0 not independently confirmed — verify locally before pinning. |
| Features | MEDIUM | Web-sourced, cross-checked across multiple independent sources per topic; no single authoritative spec exists for "save system best practices" the way there is for a language spec. |
| Architecture | HIGH (internal) / LOW (external validation) | Internal design reasoning is direct-codebase-read (HIGH); external "best practice" corroboration (ReaderWriterLockSlim vs lock, indie-game versioned-save-pattern blog) is general web search, used only to corroborate, not as the basis for the design. |
| Pitfalls | MEDIUM-HIGH | Synthesized from verified Unity/IMGUI/UniTask engineering constraints (official sources for IMGUI ordering, UniTask semantics) plus DSM-specific claims cross-referenced against the project's own `CONCERNS.md` audit. |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

- **PBKDF2 iteration count:** OWASP's current 600k-iteration guidance was web-search-summarized, not fetched directly from owasp.org — verify against the live cheat sheet and benchmark on actual minimum-spec target hardware before hardcoding a value.
- **Exact `com.unity.test-framework` version for the target Unity 6000.x Editor build:** Not independently confirmed — check `Window > Package Manager` locally before finalizing the floor version in `package.json`.
- **`ConcurrentDictionary` under IL2CPP AOT:** The decision to avoid it in favor of `Dictionary` + `lock` rests on anecdotal forum/GitHub reports, not an official Unity limitation. If profiling later shows lock contention, this decision should be revisited with platform-specific verification.
- **Migration interface granularity (Pitfall 7):** Research recommends composable per-step transforms over a full-slot-rebuild function, but the precise `IDSMMigration` contract shape wasn't fully specified — resolve during Phase 4's discuss/plan step.

## Sources

### Primary (HIGH confidence)
- `learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm` — official Microsoft API reference, confirmed `[UnsupportedOSPlatform("browser")]`
- `docs.unity3d.com/Packages/com.unity.test-framework@*/manual/` — official Unity Test Framework manual (Edit Mode vs Play Mode, async test support)
- `docs.unity3d.com/2022.3/Documentation/Manual/gui-Controls.html` + Unity Blog "Going deep with IMGUI and Editor Customization" — official IMGUI control-ordering documentation
- `github.com/Cysharp/UniTask` — official UniTask repo, async/await semantics and awaiter reuse constraints
- Direct codebase read: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/STRUCTURE.md`, `.planning/codebase/CONCERNS.md`, `.planning/PROJECT.md`

### Secondary (MEDIUM confidence)
- `github.com/dotnet/runtime` issue #92482 — AesGcm iOS/tvOS support inconsistency
- OWASP Password Storage Cheat Sheet (web-search summary) — PBKDF2 iteration guidance, needs direct verification
- Unity Discussions threads on async/await in tests — community-reported deadlock patterns
- Easy Save 3 documentation (competitor reference) — key rotation and versioning approach comparison
- "A Practical Save System for Indie Games: Versioned, Portable, Testable" — informed the envelope/migration-runner design

### Tertiary (LOW confidence)
- Unity Discussions forum posts on IL2CPP `ConcurrentDictionary`/`CryptoStream` quirks — anecdotal, used only as directional signal
- General key-rotation/envelope-encryption blog posts (KMS patterns) — used as contrast cases, explicitly not adopted for DSM's single-local-key scope

---
*Research completed: 2026-07-08*
*Ready for roadmap: yes*
