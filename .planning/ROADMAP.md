# Roadmap: DataSaveManager (DSM)

## Overview

This milestone hardens an existing, working save system rather than building one from scratch. The work follows a hard dependency chain: `DSMSlot`/`DSMSlotManager` thread-safety must land first because every other capability (encryption rotation, schema validation, migration, editor tooling) reads and writes through the exact same racy `_data` dictionary and load/save code paths — building on top of unsynchronized state would just let new features inherit the existing races. From there, encryption hardening (key validation, Encrypt-then-MAC, atomic rotation) establishes the atomic write-temp-then-rename pattern that migration later reuses. Schema validation lands next so migration has a validation target to migrate toward. Save versioning and lazy per-slot migration follow, batched into one increment since breaking the save format is already an accepted cost this milestone. The roadmap closes with a combined performance/reactivity/editor-tooling phase — batched watcher notifications, `DSMManagerWindow` decomposition and caching, and the new version/migration/rotate-key Editor UI — since all of that is consumption of the runtime APIs built in the earlier phases.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Foundation — Thread-Safety, Robustness & Test Infrastructure** - Synchronize shared slot state, fix the debounce/CTS race, make saves atomic, harden slot-name/input validation, and stand up the test framework
- [x] **Phase 2: Encryption Hardening — Key Validation & Rotation** - Centralize key validation, switch to Encrypt-then-MAC (AES-CBC + HMAC-SHA256), and add atomic key rotation (completed 2026-07-14)
- [ ] **Phase 3: Schema Validation** - Type-safe `Set`/`Get` validation built from existing codegen metadata
- [ ] **Phase 4: Save Versioning + Migration** - Versioned save envelope with lazy, composable per-slot migration on load
- [ ] **Phase 5: Performance, Reactivity & Editor Tooling** - Batched watcher notifications, `DSMManagerWindow` decomposition/caching, and new version/migration/rotate-key Editor UI

## Phase Details

### Phase 1: Foundation — Thread-Safety, Robustness & Test Infrastructure

**Goal**: DSM's shared state and save/load pipeline are safe under concurrent access and malformed or invalid input, verified by an automated test suite
**Mode:** mvp
**Depends on**: Nothing (first phase)
**Requirements**: CONC-01, CONC-02, CONC-03, CONC-04, BUGS-03, BUGS-04, TEST-01, TEST-03, TEST-04
**Success Criteria** (what must be TRUE):

  1. Concurrent `Set()` + `SaveAsync()` calls from multiple call sites do not corrupt or lose data, verified by a regression test asserting final-state correctness rather than just absence of exceptions
  2. Rapid successive `Set()` calls that repeatedly reschedule the debounce timer never throw a disposed-`CancellationTokenSource` exception, and the last-written value is always the one persisted
  3. A slot save interrupted mid-write never leaves a corrupted or partially-written file on disk — writes go through a temp-file-then-atomic-rename sequence
  4. Invalid input is rejected with a clear error instead of corrupting state or failing silently: path-traversal/reserved slot names are rejected, malformed JSON on load doesn't crash, and a widget prefab missing its `IDSMWidget` component logs a warning instead of failing silently
  5. `Tests/Editor` and `Tests/Runtime` asmdefs exist, and the full suite (concurrency regression tests, invalid-input tests, slot-name-validation tests) runs green in the Unity Test Runner

**Plans**: 3/3 plans executed

- [x] 01-01-PLAN.md
- [x] 01-02-PLAN.md
- [x] 01-03-PLAN.md

**UI hint**: yes

### Phase 2: Encryption Hardening — Key Validation & Rotation

**Goal**: Encryption keys are validated consistently everywhere, encrypted saves are tamper-evident, and rotating a key never risks losing player data
**Mode:** mvp
**Depends on**: Phase 1 (reuses the I/O gate and atomic write-temp-then-rename primitive)
**Requirements**: BUGS-02, ENC-01, ENC-02, TEST-02
**Success Criteria** (what must be TRUE):

  1. Setting an empty, null, or too-short encryption key is rejected with a clear error from both runtime and Editor code paths, through one shared validation accessor
  2. Encrypted save files use AES-CBC + HMAC-SHA256 (Encrypt-then-MAC); a tampered or truncated encrypted file fails to decrypt with a clear error instead of returning corrupted data
  3. Calling `DSM.RotateEncryptionKeyAsync(newKey)` re-encrypts every slot with the new key and only commits the new key to `DSMConfig` after all slots succeed — an interruption mid-rotation still leaves every slot readable with a consistent key, never a mixed or unrecoverable state
  4. Automated tests cover wrong key, truncated file, empty key, and key-change-between-saves scenarios, all passing

**Plans**: 2/2 plans complete
**Wave 1**

- [x] 02-01-PLAN.md — Tamper-evident encryption (AES-CBC + HMAC-SHA256 Encrypt-then-MAC) + centralized key validation (ENC-01, BUGS-02, TEST-02)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 02-02-PLAN.md — Atomic encryption key rotation with staging + journal recovery (ENC-02, TEST-02)

### Phase 3: Schema Validation

**Goal**: `Set`/`Get` calls are protected against type mismatches using the existing `DSMConstant` codegen metadata as the single source of truth
**Mode:** mvp
**Depends on**: Phase 1 (validation wraps `Set`/`Get`, which must already be concurrency-safe)
**Requirements**: SCHM-01, SCHM-02
**Success Criteria** (what must be TRUE):

  1. Calling `Set`/`Get` with a value whose type doesn't match the schema-declared type for that key is caught: warn-and-coerce in lenient mode, throw in strict mode
  2. `DSMConfig.StrictSchema` defaults to lenient (warn + coerce) so existing call sites keep working after upgrading, and can be switched to strict mode without touching call sites
  3. Schema definitions are derived from existing `DSMConstant`/codegen metadata — no second, separately-maintained source of truth for key types

**Plans**: 1 plan
**Wave 1**

- [ ] 03-01-PLAN.md — DSMSchema (reflected from DSMConstant) + DSMConfig.StrictSchema (lenient default) + Set/Get type validation (SCHM-01, SCHM-02)

### Phase 4: Save Versioning + Migration

**Goal**: Save files carry an explicit version and old saves migrate forward correctly and losslessly when loaded, without a bulk startup migration pass
**Mode:** mvp
**Depends on**: Phase 1 (atomic load/save), Phase 3 (migrated output is validated against the schema)
**Requirements**: MIGR-01, MIGR-02, MIGR-03, TEST-06
**Success Criteria** (what must be TRUE):

  1. Every slot file on disk stores a `SaveVersion` field alongside the payload in an envelope (`{ version, data }`), separate from game data
  2. A save file at an older version is migrated to the current version only when that specific slot is loaded — not for all slots at startup — and the migrated result is written back to disk immediately
  3. Migrations are expressed as small, composable per-step transforms (v1→v2→v3…) via `IDSMMigration`/`DSMMigrationRunner`, so a renamed or removed key is explicitly remapped rather than silently replaced with a default
  4. A `TestFixtures/` directory of versioned sample save files exists, and regression tests confirm each fixture migrates to the expected current-version output

**Plans**: TBD

### Phase 5: Performance, Reactivity & Editor Tooling

**Goal**: The save system performs efficiently under frequent changes and larger slot counts, and the Editor Manager window is maintainable, accurate, and exposes the new version/migration/rotation capabilities
**Mode:** mvp
**Depends on**: Phase 1, Phase 2, Phase 3, Phase 4 (the Editor panel and tests call into rotation, schema, and migration APIs from every prior phase)
**Requirements**: WATCH-01, PERF-01, PERF-02, PERF-03, PERF-04, EDIT-01, BUGS-01, TEST-05
**Success Criteria** (what must be TRUE):

  1. `WatchAsync<T>` subscribers receive at most one batched notification per frame even when `Set()` is called many times in that frame, instead of a synchronous notification on every call
  2. `DSMManagerWindow.cs` is split into focused classes (UI rendering, reflection/defaults sync, slot operations), and its reflection scan plus `GetAllSlots()` results are cached and only invalidated on relevant changes — reopening the window or adding a slot no longer re-scans assemblies or re-enumerates every slot from scratch
  3. The UniTask git dependency is pinned to a specific commit hash in `package.json` instead of tracking `main`
  4. The Editor Manager window shows each slot's save version and migration status and exposes a rotate-key action via new dedicated classes — not appended to `DSMManagerWindow.cs` — and error handling in that window surfaces specific, visible errors instead of silently swallowing exceptions
  5. Switching or deleting the selected slot in the Editor window while it's open never leaves the UI in a broken or stale state, verified by automated Editor tests

**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation — Thread-Safety, Robustness & Test Infrastructure | 3/3 | Complete | 2026-07-13 |
| 2. Encryption Hardening — Key Validation & Rotation | 2/2 | Complete   | 2026-07-14 |
| 3. Schema Validation | 0/1 | Not started | - |
| 4. Save Versioning + Migration | 0/TBD | Not started | - |
| 5. Performance, Reactivity & Editor Tooling | 0/TBD | Not started | - |
