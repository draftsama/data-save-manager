# DataSaveManager (DSM)

## What This Is

A lightweight, slot-based save system for Unity with JSON serialization, optional AES encryption, reactive change watching, and an Editor Manager window for defining typed data constants. Ships as a standalone Unity package (`com.draftsama.datasavemanager`, own git repo) consumed by Unity projects such as counter-stack-game.

## Core Value

Save/load must be correct and safe — no silent data loss, no silent decryption failures, no corruption under concurrent access.

## Requirements

### Validated

- ✓ Static API for get/set/save/load typed data — `Runtime/DSM.cs` — existing
- ✓ Multi-slot save management — `Runtime/DSMSlotManager.cs` — existing
- ✓ Shared defaults via generated `DSMConstant` — `Editor/DSMCodeGenerator.cs` — existing
- ✓ Optional AES-256 encryption with PBKDF2 — `Runtime/DSMEncryptor.cs` — existing
- ✓ Async save/load via UniTask — existing
- ✓ Reactive change watching (`WatchAsync<T>`) — `Runtime/DSMWatcher.cs` — existing
- ✓ Unity type support (Vector2/3/4, Quaternion, Color, Color32) — existing
- ✓ Editor Manager window for entries/slots/values — `Editor/DSMManagerWindow.cs` — existing
- ✓ Runtime Config Canvas with typed widgets — `Runtime/DSMRuntimePanel.cs` — existing

### Active

<!-- Scope for this milestone: comprehensive code review & hardening pass, driven by .planning/codebase/CONCERNS.md findings -->

**Bugs & Security**
- [ ] Replace broad `catch { return null; }` in `DSMManagerWindow.cs` with specific exception handling + visible errors
- [ ] Enforce encryption key validation (reject empty/short keys) in `DSMConfig`/`DSMSlot`
- [ ] Validate slot names against path traversal and reserved names in `DSMSlotManager`
- [ ] Log warnings on silent widget component errors in `DSMRuntimePanel`

**Fragile Areas**
- [ ] Add thread-safety to `DSMSlot._data` (lock or `ConcurrentDictionary`)
- [ ] Fix debounce timer disposal race in `DSMSlot.ScheduleSave()`
- [ ] Resolve hardcoded output path in `DSMCodeGenerator` dynamically instead of hardcoding `Assets/DataSaveManager/...`

**Tech Debt & Performance**
- [ ] Split `DSMManagerWindow.cs` (825 lines) into focused classes (rendering, reflection/sync, slot ops)
- [ ] Cache reflection scan in `DSMManagerWindow` instead of rescanning on every window open
- [ ] Cache `GetAllSlots()` results, invalidate only on slot create/delete
- [ ] Pin UniTask git dependency to a commit instead of tracking `main`

**Test Coverage**
- [ ] Add tests for encryption edge cases (wrong key, truncated file, empty key, key change)
- [ ] Add tests for concurrent slot operations (`Set`+`Load`, `SaveAsync`+`Get`, multi-watcher)
- [ ] Add tests for invalid inputs (null keys, empty slot names, malformed JSON)
- [ ] Add tests for Editor window state transitions (slot switch/delete while selected)

**Missing Critical Features**
- [ ] Data validation/schema — type mismatch protection on `Set`/`Get`
- [ ] Slot versioning + migration callback support
- [ ] Encryption key rotation support
- [ ] Batched/deferred watcher notifications to avoid frame drops under high change frequency

### Out of Scope

- Backward compatibility with old save files/encryption keys — breaking changes are acceptable to fix validation/security properly
- Multiplayer/networked save sync — outside this package's purpose
- Storage backends beyond Unity's `persistentDataPath` — not requested

## Context

- Standalone git repo (`github.com/draftsama/data-save-manager.git`), embedded inside `counter-stack-game/Assets/DataSaveManager` but tracked independently.
- v1.0.0, Unity 6000.0+, C# 11+. Depends on UniTask (git dependency) and `com.unity.nuget.newtonsoft-json` 3.2.1.
- No automated test framework currently in place — introducing one is part of this milestone's scope.
- Full codebase analysis available in `.planning/codebase/` (STACK.md, ARCHITECTURE.md, STRUCTURE.md, CONVENTIONS.md, TESTING.md, CONCERNS.md, INTEGRATIONS.md) — CONCERNS.md is the direct source for this milestone's Active requirements.

## Constraints

- **Tech stack**: Unity 6000.0+, C# 11+, UniTask, Newtonsoft.Json — stay within existing stack
- **Compatibility**: No backward-compat requirement for old save files or encryption keys — confirmed by user, breaking changes allowed
- **Testing**: No test framework detected yet — must be introduced to satisfy Test Coverage requirements

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Treat DataSaveManager as a standalone GSD project with its own `.planning/` | It is its own git repo with its own remote, separate from the parent counter-stack-game project's planning | — Pending |
| No backward compatibility required for old saves/keys | User confirmed breaking changes are acceptable to fix security/validation issues correctly | — Pending |
| Include "Missing Critical Features" (schema validation, versioning, key rotation, batched watchers) in v1 scope | User wants comprehensive hardening, not just bug fixes | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-07-08 after initialization*
