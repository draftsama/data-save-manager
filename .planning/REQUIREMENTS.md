# Requirements: DataSaveManager (DSM)

**Defined:** 2026-07-08
**Core Value:** Save/load must be correct and safe — no silent data loss, no silent decryption failures, no corruption under concurrent access.

## v1 Requirements

Requirements for this hardening milestone. Each maps to roadmap phases.

### Bugs & Security

- [ ] **BUGS-01**: Replace broad `catch { return null; }` in `DSMManagerWindow.cs` with specific exception handling and visible, user-facing errors
- [x] **BUGS-02**: Centralize encryption key validation (reject empty/short keys) behind a single accessor used by both runtime and Editor
- [ ] **BUGS-03**: Validate slot names against path traversal and Windows-reserved names in `DSMSlotManager`
- [ ] **BUGS-04**: Log a warning when a widget prefab is instantiated without an `IDSMWidget` component in `DSMRuntimePanel`

### Concurrency & Reliability

- [x] **CONC-01**: Make `DSMSlot._data` and `DSMSlotManager._slots` thread-safe (synchronized collections + an I/O gate for compound Load/Save/Rotate operations spanning `await`)
- [x] **CONC-02**: Fix the debounce-timer / `CancellationTokenSource` disposal race in `DSMSlot.ScheduleSave()` (same root cause as CONC-01 — sequence together)
- [x] **CONC-03**: Atomic write-temp-then-rename for all slot save operations (foundational prerequisite for key rotation and migration)
- [x] **CONC-04**: Concurrency regression tests asserting final-state correctness, not just absence of exceptions

### Encryption Hardening

- [x] **ENC-01**: Switch encryption from AES-GCM-style expectations to AES-CBC + HMAC-SHA256 (Encrypt-then-MAC) — fully managed, works on every Unity target platform including WebGL
- [x] **ENC-02**: Encryption key rotation (`DSM.RotateEncryptionKeyAsync`) via atomic decrypt-with-old-key / re-encrypt-with-new-key per slot; commit new key to `DSMConfig` only after all slots succeed

### Schema Validation

- [x] **SCHM-01**: Type-safe validation on `Set`/`Get` against a schema derived from existing `DSMConstant` codegen metadata (new `DSMSchema` component)
- [x] **SCHM-02**: `DSMConfig.StrictSchema` flag, defaulting to lenient (warn + coerce) to avoid breaking existing call sites on upgrade

### Versioning & Migration

- [ ] **MIGR-01**: `SaveVersion` field in the slot file envelope (`{ version, data }`), separate from the payload
- [ ] **MIGR-02**: `IDSMMigration` interface + `DSMMigrationRunner` — composable per-step transforms (v1→v2→v3…), not a single monolithic rebuild function
- [ ] **MIGR-03**: Lazy migration on load (migrate a slot only when it's loaded, write the migrated result back immediately) — not bulk migration at startup

### Reactive Watching

- [ ] **WATCH-01**: Batched/per-frame-flush notifications for `WatchAsync<T>` instead of synchronous notification on every `Set()`

### Tech Debt & Performance

- [ ] **PERF-01**: Split `DSMManagerWindow.cs` (825 lines) into focused classes (UI rendering, reflection/defaults sync, slot operations)
- [ ] **PERF-02**: Cache the reflection scan in `DSMManagerWindow` instead of rescanning assemblies on every window open
- [ ] **PERF-03**: Cache `GetAllSlots()` results in `DSMSlotManager`, invalidate only on slot create/delete
- [ ] **PERF-04**: Pin the UniTask git dependency to a commit hash instead of tracking `main`

### Testing

- [x] **TEST-01**: Stand up `Tests/Editor` and `Tests/Runtime` asmdefs using Unity Test Framework (NUnit) — this package currently has zero automated tests
- [x] **TEST-02**: Tests for encryption edge cases (wrong key, truncated file, empty key, key change between saves/loads)
- [x] **TEST-03**: Tests for concurrent slot operations (`Set`+`Load`, `SaveAsync`+`Get`, multi-watcher)
- [ ] **TEST-04**: Tests for invalid inputs (null keys, empty slot names, malformed JSON)
- [ ] **TEST-05**: Tests for Editor window state transitions (slot switch/delete while selected)
- [ ] **TEST-06**: `TestFixtures/` directory of versioned sample save files, with regression tests, so migration logic doesn't silently rot

### Editor Tooling

- [ ] **EDIT-01**: New, separate Editor classes for version/migration status and the rotate-key action — not appended to `DSMManagerWindow.cs`

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Encryption Hardening

- **ENC-03**: `HasEncryptionKey` introspection API on `DSMConfig`

### Versioning & Migration

- **MIGR-04**: Migration dry-run / pre-migration backup (`.bak` file before migrating)
- **MIGR-05**: Expand-switch-contract key-rename fallback (read old key for one version before removing)

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Backward compatibility with pre-v1.0 save files/encryption format | User confirmed breaking changes are acceptable; carrying forward the insecure fixed-salt format would defeat the purpose of this milestone |
| Multiplayer/networked save sync | Outside this package's purpose |
| Storage backends beyond Unity's `persistentDataPath` | Not requested |
| Full envelope encryption (DEK/KEK key hierarchy) | Solves a multi-tenant/server-side problem DSM doesn't have; over-engineered for a local single-player save file |
| LRU/manual slot-cache eviction | No evidence of memory pressure from many slots; revisit if a real use case emerges |
| Cross-process file locking | DSM's concurrency model is in-process only (main thread vs. background save task), not multi-process |

## Traceability

Which phases cover which requirements. Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| CONC-01 | Phase 1 | Complete |
| CONC-02 | Phase 1 | Complete |
| CONC-03 | Phase 1 | Complete |
| CONC-04 | Phase 1 | Complete |
| TEST-01 | Phase 1 | Complete |
| BUGS-02 | Phase 2 | Complete |
| ENC-01 | Phase 2 | Complete |
| ENC-02 | Phase 2 | Complete |
| BUGS-01 | Phase 5 | Pending |
| BUGS-03 | Phase 1 | Pending |
| BUGS-04 | Phase 1 | Pending |
| SCHM-01 | Phase 3 | Complete |
| SCHM-02 | Phase 3 | Complete |
| MIGR-01 | Phase 4 | Pending |
| MIGR-02 | Phase 4 | Pending |
| MIGR-03 | Phase 4 | Pending |
| TEST-06 | Phase 4 | Pending |
| WATCH-01 | Phase 5 | Pending |
| EDIT-01 | Phase 5 | Pending |
| PERF-01 | Phase 5 | Pending |
| PERF-02 | Phase 5 | Pending |
| PERF-03 | Phase 5 | Pending |
| PERF-04 | Phase 5 | Pending |
| TEST-02 | Phase 2 | Complete |
| TEST-03 | Phase 1 | Complete |
| TEST-04 | Phase 1 | Pending |
| TEST-05 | Phase 5 | Pending |

**Coverage:**

- v1 requirements: 27 total
- Mapped to phases: 27
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-08*
*Last updated: 2026-07-08 after roadmap creation (27/27 requirements mapped across 5 phases)*
