# Phase 5: Performance, Reactivity & Editor Tooling - Context

**Gathered:** 2026-07-21
**Status:** Ready for planning
**Source:** Inline decision capture during /gsd-plan-phase (no separate discuss-phase; research disabled in config)

<domain>
## Phase Boundary

Phase 5 hardens an already-correct save library for scale and maintainability. It does NOT change the save format, encryption, schema, or migration semantics shipped in Phases 1–4. It delivers five slices:

1. **Reactivity (WATCH-01)** — `WatchAsync<T>` currently notifies synchronously on every `Set()` (`DSMWatcher.Notify` → `Channel.TryWrite`, called from `DSMSlot.Set:93`). Make notifications batched: at most one per key per frame.
2. **Runtime/dependency hardening (PERF-03, PERF-04)** — `DSMSlotManager.GetAllSlots()` re-scans the save directory on every call; cache it. The UniTask UPM dependency tracks a moving branch; pin it.
3. **Editor decomposition (PERF-01, PERF-02, BUGS-01)** — `Editor/DSMManagerWindow.cs` is 825 lines, re-scans all assemblies for `DSMConstant` defaults on every window open, and swallows exceptions with `catch { return null; }`. Split into focused classes, cache the reflection scan, surface visible errors.
4. **Editor version/migration/rotate UI (EDIT-01)** — expose each slot's save version + migration status and a rotate-key action via NEW, separate Editor classes (never appended to `DSMManagerWindow.cs`).
5. **Editor state tests (TEST-05)** — automated EditMode tests that switching/deleting the selected slot while the window is open never leaves stale/broken UI.

Out of scope: no save-format change, no new runtime encryption/schema/migration behavior, no v2 requirements.
</domain>

<decisions>
## Implementation Decisions

- **D-01 — WATCH-01 batching: coalesce to latest value (LOCKED, user-chosen).**
When `Set()` is called many times for the same key within one frame, `WatchAsync<T>` subscribers receive exactly ONE notification carrying the **latest** value; intermediate values are dropped. This directly matches ROADMAP success criterion 1 ("at most one batched notification per frame"). Rejected alternative: queue-and-deliver-all-values-in-one-drain (higher fidelity, more allocation) — not chosen.

- **D-02 — WATCH-01 flush: dirty-flag + schedule-once per frame via UniTask PlayerLoop (LOCKED).**
`DSMWatcher` keeps a per-key pending-latest buffer. The first `Notify` in a frame marks the watcher dirty and schedules a single flush on the next PlayerLoop tick (`UniTask.Yield(PlayerLoopTiming.PostLateUpdate)` or equivalent); further `Notify`s in the same frame only overwrite the buffered value. An `internal void Flush()` drains the buffer (one write per key to each channel) so EditMode tests can invoke a frame boundary deterministically without depending on a running PlayerLoop. Edit-mode caveat: the scheduled flush only fires under a running PlayerLoop (play mode); this is acceptable because `WatchAsync` is a runtime/game API, and tests call `Flush()` directly.

- **D-03 — PERF-04 pin: latest stable UniTask release tag (LOCKED, user-chosen).**
Pin `com.cysharp.unitask` to UniTask's newest **stable release tag** (e.g. `#2.5.x`) via the UPM git `#<ref>` suffix, not to a raw commit SHA. Immutable, human-readable, easy to bump. The requirement text says "commit hash"; a release tag is an equally-immutable pin that better satisfies the intent ("stop tracking `main`"). Rejected alternative: 40-char commit SHA — not chosen (opaque, harder to audit/bump).

- **D-04 — PERF-03 cache invalidation: create/delete through the manager (LOCKED).**
`DSMSlotManager` caches the `GetAllSlots()` result and invalidates it only on `DeleteSlot` and when `GetOrCreateSlot` first registers a new slot — matching the requirement's own "invalidate only on slot create/delete" wording. The first `GetAllSlots()` still scans disk once (so pre-existing on-disk save files are picked up); thereafter the cache tracks slots created/deleted through the manager. Filesystem changes made outside the manager are not observed until the next create/delete — documented behavior, acceptable for editor/runtime use.

- **D-05 — Editor decomposition: extract non-UI logic into standalone classes (LOCKED).**
`DSMManagerWindow` becomes a thin window (lifecycle + `OnGUI` + `Draw*` rendering) that composes two new Editor classes: `DSMConstantReflectionCache` (the cached `DSMConstant` reflection scan + type mapping) and `DSMManagerSlotOps` (slot discover/load/write/delete/propagate/commit). This makes the slot-state logic unit-testable (TEST-05) without driving IMGUI.

- **D-06 — EDIT-01 version read is read-only, no side effects (LOCKED).**
Version/migration status is displayed by reading each slot file **without** triggering migration or write-back. A small read-only Runtime helper `DSMSaveInspector.TryReadOnDiskVersion(path, config, out version)` reuses `DSMEncryptor.Decrypt` (for encrypted slots) + `DSMSaveEnvelope.TryUnwrap` and returns the on-disk version only. "Needs migration" = on-disk version < `DSM.CurrentSaveVersion` (new read-only accessor over `DSMSlotManager._migrationRunner.CurrentVersion`).

- **D-07 — Rotate-key UI is guarded (LOCKED).**
The rotate-key action confirms via `EditorUtility.DisplayDialog` before running (destructive/atomic), disables the panel while the async rotation is in flight, and surfaces success and every failure (including `DSMRotationInterruptedException`) visibly (`EditorUtility.DisplayDialog` + `Debug.LogError`). The new key is never written to any log. This is the BUGS-01 "visible errors" principle applied to the new UI.

- **D-08 — CR-01 fold-in is TEST-ONLY (LOCKED, user-chosen; scope corrected during planning).**
User chose to close the lingering Phase-03 CR-01 item inside Phase 5. **Correction found during planning:** CR-01 is NOT "plaintext password logging in DSMEncryptor" (that memory was inaccurate — `Runtime/DSMEncryptor.cs` contains no `Debug.Log` at all; its exception messages are generic and value-free). Per `03-REVIEW.md` / `03-REVIEW-FIX.md`, CR-01 is the lenient coercion-failure warning that once leaked `ex.Message` at `DSMSlot.cs:57` — and its **code fix is already shipped** (commit d04f0da; the log is now value-free). The genuinely open item is the **deferred regression test** guarding the no-value-leak invariant, plus the related WR-05 test (a coerced `Set` must reach a schema-type `WatchAsync<T>` subscriber). Both are folded into plan 05-01 (they live in the `Set`/watcher domain). No production code changes for CR-01.

### Claude's Discretion
- Exact `PlayerLoopTiming` used for the WATCH-01 flush schedule; exact file/line placement of extracted Editor methods; IMGUI layout and labels of the new panels; the assembly-reload invalidation callback (`AssemblyReloadEvents.afterAssemblyReload` vs `[DidReloadScripts]`); the precise editor-thread mechanism used to observe UniTask rotation completion.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Reactivity (WATCH-01, WR-05)
- `Runtime/DSMWatcher.cs` — `Notify`/`Watch`/`Register`/`Unregister`; the channel-per-subscriber model to add batching to
- `Runtime/DSMSlot.cs` (Set: lines 41-96, WatchAsync: 410-427) — the `_watcher.Notify(key, notifyValue)` call site + the coerced-notify path (WR-05)

### Runtime/deps (PERF-03, PERF-04)
- `Runtime/DSMSlotManager.cs` (GetAllSlots: 62-78, DeleteSlot: 48-60, GetOrCreateSlot: 251-259) — enumeration + create/delete points to cache/invalidate
- `package.json` (line 11) — the UniTask UPM git dependency to pin

### Editor (PERF-01, PERF-02, BUGS-01, EDIT-01, TEST-05)
- `Editor/DSMManagerWindow.cs` — 825-line window; `LoadDefaultsFromReflection:92` (assembly scan), `catch { return null; }:212`, all `Draw*`/slot-ops methods
- `Editor/DSM.Editor.asmdef` — Editor assembly (Editor-only platform; references Runtime GUID); NOT compiled by `dotnet build` — verify editor code via grep + human Unity compile
- `Runtime/DSMEncryptor.cs` — `Decrypt(byte[], string)` for the read-only version inspector (EDIT-01)
- `Runtime/DSMSaveEnvelope.cs` — `TryUnwrap` for reading on-disk version (EDIT-01)
- `Runtime/DSMMigrationRunner.cs` — `CurrentVersion` for migration-status comparison (EDIT-01)
- `Runtime/DSM.cs` / `Runtime/DSMSlotManager.cs` — `RotateEncryptionKeyAsync` (rotate action) + where to expose `CurrentSaveVersion`

### Phase-03 carryover (D-08)
- `.planning/phases/03-schema-validation/03-REVIEW-FIX.md` — CR-01 already-applied fix + the deferred CR-01/WR-05 regression tests to add
- `Tests/Editor/DSMSchemaValidationTests.cs` — `Strict_ExceptionMessage_DoesNotLeakOffendingValue` is the pattern to mirror for the CR-01 lenient-path test

### Test conventions
- `Tests/Editor/DSMSlotConcurrencyTests.cs`, `Tests/Editor/DSMMigrationTests.cs`, `Tests/Editor/DSMTestConfig.cs` — NUnit, global namespace, `#nullable enable`, temp-dir SetUp/TearDown, `DSMTestConfig` builder, direct `new DSMSlot(...)`, `LogAssert` usage; NO `Assert.ThrowsAsync`

### Project instructions (mandatory)
- `.claude/CLAUDE.md` — no-comment default; NEVER run Unity tests (human runs Test Runner)
</canonical_refs>

<specifics>
## Specific Ideas

- WATCH-01 flush is per-`DSMWatcher` self-scheduling (dirty flag + one scheduled flush), not a global watcher registry.
- PERF-03 cache is a nullable `string[]? _allSlotsCache` guarded by the existing `_slotsLock`.
- Editor decomposition uses standalone composed classes (not just `partial`), so slot-state logic is testable by TEST-05.
- The DMS.Editor / DMS.Tests.Editor assemblies are human-compiled/-run in Unity; `dotnet build DMS.Runtime.csproj` only covers Runtime. Editor-side automated verification is grep + structure assertions.
- New `.cs` files need `.cs.meta` — Unity generates these on import; the human compile step covers meta generation.
</specifics>

<deferred>
## Deferred Ideas

- **WR-01/WR-02/WR-03/WR-04** (Phase 03 warnings) — already fixed in `03-REVIEW-FIX.md`; not re-touched here.
- **TEST-04** (invalid-input tests: null keys, empty slot names, malformed JSON) — mapped to Phase 1 scope, not Phase 5.
- Any DSMEncryptor change — none needed (no secret logging exists; D-08).
- Editor UI visual redesign beyond exposing version/migration/rotate — out of scope.
</deferred>

---

*Phase: 05-performance-reactivity-editor-tooling*
*Context captured: 2026-07-21 inline during /gsd-plan-phase*
