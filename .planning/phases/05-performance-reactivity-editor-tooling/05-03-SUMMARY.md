---
phase: 05-performance-reactivity-editor-tooling
plan: 03
subsystem: editor-tooling
tags: [editorwindow, imgui, reflection-cache, refactor, error-handling, unity-editor]

requires:
  - phase: 01-foundation
    provides: DSMManagerWindow, DSMConstant reflection defaults, DSMDataEntry model
  - phase: 02-encryption
    provides: DSMEncryptor read/write path the slot ops call
provides:
  - DSMManagerSlotOps — headless slot state + operations the 05-05 tests drive
  - DSMConstantReflectionCache — assembly-reload-invalidated DSMConstant scan
  - Visible, specific Editor error surfacing (LastError + HelpBox + Debug.LogError)
affects: [05-04-editor-panels, 05-05-editor-state-tests]

tech-stack:
  added: []
  patterns:
    - "Editor god-class split into window (rendering) + ops (state/IO) + cache (reflection)"
    - "Confirmation dialogs stay in the window so the ops class stays headless and testable"
    - "Static editor cache invalidated only by AssemblyReloadEvents.afterAssemblyReload"

key-files:
  created:
    - Editor/DSMConstantReflectionCache.cs
    - Editor/DSMManagerSlotOps.cs
  modified:
    - Editor/DSMManagerWindow.cs

key-decisions:
  - "GetDefaults() returns a deep copy of the cached scan because the window mutates its defaults list in place (type edits, runtime-key sync, add/remove)"
  - "EditorUtility.DisplayDialog confirmations stayed in the window; DSMManagerSlotOps does the work, so 05-05 can drive it without a blocking dialog"
  - "Error messages carry operation + slot + exception type only, never ex.Message, matching the runtime's no-value-leak rule"
  - "Verified by compiling DSM.Editor.csproj with dotnet (0 errors) rather than grep alone"

patterns-established:
  - "Fail(operation, slot, ex) helper centralises log + LastError so no catch can silently return"
  - "Static conversion helpers (InferFromToken, EntryToJToken) live with the ops class and are called statically by the window"

requirements-completed: [PERF-01, PERF-02, BUGS-01]

coverage:
  - id: D1
    description: "DSMManagerWindow.cs is decomposed into window + reflection cache + slot ops with unchanged observable behavior"
    requirement: PERF-01
    verification:
      - kind: other
        ref: "dotnet build DSM.Editor.csproj -clp:ErrorsOnly → 0 errors; window 825 → 565 lines; moved-method greps"
        status: pass
    human_judgment: true
    rationale: "Behavior parity of an IMGUI window (discover/select/add/delete/propagate) can only be confirmed by driving the window in Unity"
  - id: D2
    description: "The DSMConstant assembly scan runs once and is reused across window opens, re-running only after an assembly reload"
    requirement: PERF-02
    verification:
      - kind: other
        ref: "grep: static s_cache + GetDefaults + AssemblyReloadEvents.afterAssemblyReload; window Reload() reads via GetDefaults()"
        status: pass
    human_judgment: true
    rationale: "Proving no rescan on reopen, and that a newly added DSMConstant field still appears after a domain reload, needs the Unity Editor"
  - id: D3
    description: "Editor slot failures are specific, logged with context and visible in the window instead of silently returning null"
    requirement: BUGS-01
    verification:
      - kind: other
        ref: "grep for `catch { return null; }` / bare catch across both Editor files → 0; Debug.LogError + LastError + HelpBox present"
        status: pass
    human_judgment: true
    rationale: "Requires forcing a real failure (corrupt or locked slot file) in the Editor to confirm the HelpBox and log appear"

duration: 22 min
completed: 2026-07-22
status: complete
---

# Phase 5 Plan 03: Editor Window Decomposition Summary

**The 825-line `DSMManagerWindow` is now a 565-line rendering shell over `DSMManagerSlotOps` (slot state + I/O) and `DSMConstantReflectionCache` (assembly-reload-invalidated scan), with silent `catch { return null; }` replaced by typed, logged, on-window errors.**

## Performance

- **Duration:** 22 min
- **Started:** 2026-07-22T14:26:00Z
- **Completed:** 2026-07-22T14:48:00Z
- **Tasks:** 3
- **Files modified:** 3 (2 created, 1 rewritten)

## Accomplishments
- **PERF-01:** responsibilities split three ways — `DSMConstantReflectionCache` (DSMConstant scan, `FieldToType`, `ValueToSerialized`, `GetTypeDefault`), `DSMManagerSlotOps` (defaults/slot-data/active-slot state plus `DiscoverSlots`, `LoadSlotData`, `SyncRuntimeKeys`, `ReadSlotJObject`, `WriteSlotJObject`, `DeleteActiveSlot`, `CreateSlot`, `SelectSlot`, `SetDefaultSlot`, `PropagateToAllSlots`, `CommitNewEntry`, and the JToken inference/conversion helpers), and the window (lifecycle + `OnGUI` + `Draw*` + config asset).
- **PERF-02:** the `AppDomain.CurrentDomain.GetAssemblies()` walk now runs once per domain behind `s_cache`, invalidated only by `AssemblyReloadEvents.afterAssemblyReload`. `Reload()` reads defaults through `DSMConstantReflectionCache.GetDefaults()` instead of rescanning on every open.
- **BUGS-01:** the read path catches `JsonException`, `IOException`, then a general fallback; each logs a specific `Debug.LogError` and sets `LastError`, which the window renders as an error `HelpBox`. Writes and deletes got the same handling. A successful operation clears the error.
- Method bodies were moved, not rewritten — the receiver changed (`this.X()` → `_slotOps.X()` / `DSMManagerSlotOps.X()`), the logic did not.
- Verified beyond grep: `dotnet build DSM.Editor.csproj` compiles the whole Editor assembly with **0 errors**.

## Task Commits

1. **Tasks 1–3: decomposition + reflection cache + error surfacing** — `c911759` (refactor)

_The three tasks landed as one atomic commit — see Deviations._

## Files Created/Modified
- `Editor/DSMConstantReflectionCache.cs` — `[InitializeOnLoad] internal static` cache; `GetDefaults()` (copy-on-read), `BuildFromReflection()`, `FieldToType`, `ValueToSerialized`, `GetTypeDefault`
- `Editor/DSMManagerSlotOps.cs` — slot-editing state + all slot operations + JToken inference/conversion + the `Fail()` error helper
- `Editor/DSMManagerWindow.cs` — 825 → 565 lines; lifecycle, `OnGUI`, `Draw*`, `DrawError`, add-panel transient input state, config asset creation

## Decisions Made
- **`GetDefaults()` returns a deep copy.** The window mutates its defaults list (type edits, `SyncRuntimeKeys` appending runtime keys, add/remove entries). Handing out the cached list itself would let one window session poison the cache for the next open.
- **Confirmation dialogs stayed in the window.** `EditorUtility.DisplayDialog` in `DeleteActiveSlot`/remove-entry is UI, and a blocking dialog inside the ops class would make plan 05-05's headless state tests impossible.
- **Errors never carry `ex.Message`.** Newtonsoft and IO messages can embed save content and full paths; the message shows operation + slot + exception type, matching the no-value-leak rule Phase 3 established for the runtime (CR-01).
- **Two extra ops methods.** `CreateSlot(name)` and `SelectSlot(name)` were extracted from the window's inline button handlers so slot transitions are one testable call — the same family as the eight methods the plan named.
- **`Fs`/`Pf` formatting helpers are duplicated** (private, 1–2 lines each) in the window, the ops class, and the cache rather than introducing a fourth shared type.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Tasks 1–3 committed atomically instead of one commit per task**
- **Found during:** Task 1 (decomposition)
- **Issue:** Tasks 2 (cache) and 3 (error handling) modify lines *inside* the two files Task 1 creates. Committing Task 1 first would mean writing the un-cached, silently-failing versions and immediately rewriting them — three commits describing one indivisible refactor.
- **Fix:** One commit whose message documents all three requirement IDs and what each changed.
- **Files modified:** Editor/DSMConstantReflectionCache.cs, Editor/DSMManagerSlotOps.cs, Editor/DSMManagerWindow.cs
- **Verification:** `dotnet build DSM.Editor.csproj -clp:ErrorsOnly` → 0 errors; all three tasks' acceptance greps pass on the final state.
- **Committed in:** `c911759`

**2. [Rule 1 - Verification correctness] Task 1's `LoadDefaultsFromReflection` grep does not match the final state**
- **Found during:** Task 2 (reflection cache)
- **Issue:** Task 1's automated check greps for the method name `LoadDefaultsFromReflection`, but Task 2's own action prescribes renaming that body to `BuildFromReflection` behind `GetDefaults()`. The two checks contradict once both tasks land.
- **Fix:** Verified the end state against Task 2's contract (`s_cache`, `GetDefaults`, `BuildFromReflection`, reload hook) plus `FieldToType`, which both tasks agree on.
- **Files modified:** none (verification only)
- **Verification:** greps for `s_cache`, `GetDefaults`, `BuildFromReflection`, `FieldToType`, `afterAssemblyReload` all pass.
- **Committed in:** `c911759`

**3. [Rule 3 - Blocking] DSM.Editor.csproj needed the new files registered**
- **Found during:** Task 1 (decomposition)
- **Issue:** The generated `DSM.Editor.csproj` lists sources explicitly, so a compile check would silently skip the two new files.
- **Fix:** Added both `<Compile Include>` entries, enabling a real Editor-assembly compile check instead of grep-only verification. (The csproj is git-ignored, like `DMS.Runtime.csproj`.)
- **Files modified:** DSM.Editor.csproj (untracked/ignored)
- **Verification:** `dotnet build DSM.Editor.csproj -clp:ErrorsOnly` → Build succeeded, 0 errors.
- **Committed in:** n/a (file is git-ignored)

---

**Total deviations:** 3 auto-fixed (1 missing critical, 1 verification correctness, 1 blocking)
**Impact on plan:** No scope change. The refactor's shape, the cache contract, and the error handling are exactly as planned; only commit granularity and one stale grep differ.

## Issues Encountered
- One pre-existing `CS8602` nullable warning moved with `GetSlotName()` from the window into `DSMManagerSlotOps` (line 42). The body was kept byte-identical per the plan's move-don't-rewrite rule, so the warning moved with it. It is a false positive — `string.IsNullOrEmpty(_config?.DefaultSlot)` guards the dereference. Adding `_config!` would silence it if the warning becomes noisy.

## User Setup Required
None - no external service configuration required.

## Verification Status

- **Automated (no Unity):** `dotnet build DSM.Editor.csproj -clp:ErrorsOnly` → Build succeeded, 0 errors (stronger than the plan's grep-only check). `dotnet build DMS.Runtime.csproj` unaffected — no Runtime file changed (`git status Runtime/` clean). Window is 565 lines (< 600). Greps confirm the moved method sets, `s_cache` + `GetDefaults` + `afterAssemblyReload`, the window's use of `GetDefaults()`, zero `catch { return null; }`/bare catches in both Editor files, and `Debug.LogError` + `LastError` + `HelpBox` wiring.
- **Human (pending — Unity):** Let the Editor assembly compile in Unity, then open **DSM ▸ Open Manager** and confirm parity: slot discovery, selecting a slot loads its data, `+ New` creates a slot seeded with defaults, `Delete` still confirms then removes, `+ New Entry` propagates to all slots, `✕` removes a key everywhere, `Set Default` writes the config, and `Save DSMConstant.cs` still generates. Close and reopen the window — defaults still show without a rescan; add a field to `DSMConstant` and confirm it appears after the domain reload. Force a failure (corrupt or lock a slot file) and confirm the red HelpBox plus a specific Console error appear, with no save content or path in the message. This is the PERF-01/PERF-02/BUGS-01 acceptance gate.

## Next Phase Readiness
- Wave 1 is complete. 05-04 can add `DSMSlotVersionPanel`/`DSMKeyRotationPanel` alongside the thinned window, and 05-05 can drive `DSMManagerSlotOps` headlessly — `SelectSlot`, `DeleteActiveSlot`, `CreateSlot`, `AvailableSlots`, `SlotData`, `Defaults` and `LastError` are the state surface those tests need.
- `DSMManagerSlotOps` is `internal` to `DSM.Editor`. Plan 05-05's tests live in `DMS.Tests.Editor`, which does **not** reference `DSM.Editor` today — 05-05 must add that assembly reference (and, since the class is internal, an `InternalsVisibleTo` on the Editor assembly or a public visibility bump).

---
*Phase: 05-performance-reactivity-editor-tooling*
*Completed: 2026-07-22*
