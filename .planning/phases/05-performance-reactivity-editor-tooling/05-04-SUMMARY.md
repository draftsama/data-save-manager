---
phase: 05-performance-reactivity-editor-tooling
plan: 04
subsystem: editor-tooling
tags: [save-versioning, migration-status, key-rotation, unitask, editor-panels, security]

requires:
  - phase: 02-encryption
    provides: DSMEncryptor.Decrypt, RotateEncryptionKeyAsync, DSMRotationInterruptedException
  - phase: 04-save-versioning-migration
    provides: DSMSaveEnvelope.TryUnwrap/LegacyVersion, DSMMigrationRunner.CurrentVersion
  - phase: 05-performance-reactivity-editor-tooling
    provides: the decomposed DSMManagerWindow + DSMManagerSlotOps that host the panels (05-03)
provides:
  - DSMSaveInspector — side-effect-free on-disk save version reader
  - DSM.CurrentSaveVersion / DSMSlotManager.CurrentVersion accessors
  - DSMSlotVersionPanel — per-slot version + migration status UI
  - DSMKeyRotationPanel — confirmed, guarded rotate-key action
affects: [05-05-editor-state-tests, future-migration-tooling]

tech-stack:
  added: []
  patterns:
    - "Read-only inspector shared by Editor UI so decrypt+unwrap logic is never duplicated in Editor code"
    - "Async Editor action guarded by confirm dialog + in-flight flag reset in a finally"

key-files:
  created:
    - Runtime/DSMSaveInspector.cs
    - Editor/DSMSlotVersionPanel.cs
    - Editor/DSMKeyRotationPanel.cs
    - Tests/Editor/DSMSaveInspectorTests.cs
  modified:
    - Runtime/DSMSlotManager.cs
    - Runtime/DSM.cs
    - Editor/DSMManagerWindow.cs
    - Editor/DSMManagerSlotOps.cs

key-decisions:
  - "A readable save with no envelope reports true + LegacyVersion (a real v1 save), so the panel flags it as needing migration rather than as unreadable"
  - "Encryption is decided by file extension first (.enc/.json) and only falls back to config.Encrypt for other names, so a leftover plain .json under an encrypting config still reads"
  - "The inspector's catch-all returns false rather than throwing — the false IS the error signal, keeping exceptions out of the Editor render loop"
  - "Version readings are cached per refresh (reload/create/delete), not re-read every OnGUI frame"
  - "DSMSaveInspectorTests compares against the local manager's CurrentVersion, not DSM.CurrentSaveVersion, to avoid building the global manager against the project's real save directory"

patterns-established:
  - "Editor panels are dedicated classes the window composes; the window gains rendering calls only"
  - "Secrets entered in the Editor flow only into the API call — never into logs, dialogs, or EditorPrefs"

requirements-completed: [EDIT-01]

coverage:
  - id: D1
    description: "Slot save version is read without side effects — no migration, no write-back, no throw on missing/corrupt/undecryptable files"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSaveInspectorTests.cs#TryReadOnDiskVersion_EnvelopeFile_ReturnsEnvelopeVersion, _LegacyFlatFile_ReturnsLegacyVersion, _MissingFile_ReturnsFalse, _CorruptFile_ReturnsFalseWithoutThrowing, _EncryptedSlot_DecryptsAndReadsVersion, _EncryptedSlotWithWrongKey_ReturnsFalse, _DoesNotMutateTheFile"
        status: unknown
    human_judgment: true
    rationale: "Project rule forbids running Unity tests from the agent; the EditMode fixture is the human gate (it includes the byte+mtime no-mutation assertion for T-05-01)"
  - id: D2
    description: "The Editor window shows each slot's on-disk version and migration status through a dedicated DSMSlotVersionPanel class"
    requirement: EDIT-01
    verification:
      - kind: other
        ref: "dotnet build DSM.Editor.csproj -clp:ErrorsOnly → 0 errors; grep: class in its own file, uses TryReadOnDiskVersion + CurrentSaveVersion, not declared in DSMManagerWindow.cs"
        status: pass
    human_judgment: true
    rationale: "Correct status rendering across a mix of current and stale slots can only be seen by opening the window in Unity"
  - id: D3
    description: "The window exposes a rotate-encryption-key action through a dedicated DSMKeyRotationPanel, confirmed and guarded, with visible success and failure"
    requirement: EDIT-01
    verification:
      - kind: other
        ref: "grep: DisplayDialog + _rotating guard + RotateEncryptionKeyAsync present; class not declared in DSMManagerWindow.cs"
        status: pass
    human_judgment: true
    rationale: "The confirm → disable → rotate → report flow and its failure path need a real rotation in the Editor"
  - id: D4
    description: "The new encryption key never reaches a log, dialog, or EditorPrefs"
    verification:
      - kind: other
        ref: "grep -nE 'Debug\\.Log.*_newKey|DisplayDialog\\([^)]*_newKey|EditorPrefs' Editor/DSMKeyRotationPanel.cs → no matches; _newKey occurs only in the field, the PasswordField, the empty check, the rotation call, and the reset"
        status: pass
    human_judgment: false

duration: 26 min
completed: 2026-07-22
status: complete
---

# Phase 5 Plan 04: Editor Version and Key-Rotation Panels Summary

**A read-only `DSMSaveInspector` (decrypt → unwrap → version, zero side effects) feeds a `DSMSlotVersionPanel` that flags stale saves, alongside a `DSMKeyRotationPanel` that runs `DSM.RotateEncryptionKeyAsync` behind a confirmation dialog and an in-flight guard without ever logging the key.**

## Performance

- **Duration:** 26 min
- **Started:** 2026-07-22T14:48:00Z
- **Completed:** 2026-07-22T15:14:00Z
- **Tasks:** 3
- **Files modified:** 8 (4 created, 4 modified)

## Accomplishments
- `DSMSaveInspector.TryReadOnDiskVersion` reads a slot's on-disk version — decrypting `.enc` files through the same `DSMEncryptor` path the runtime uses — and returns `false` with `LegacyVersion` for missing, undecryptable, or unparseable files. It contains no `Migrate`/`Save`/`WriteJsonToDisk` call, so a status display cannot mutate the save it reports on (T-05-01).
- `DSMSlotManager.CurrentVersion` and `DSM.CurrentSaveVersion` expose the migration runner's target version, so the panel compares against exactly what the runtime would migrate to.
- `DSMSlotVersionPanel` renders `Up to date (vN)` / `Needs migration: vX → vN` / `Unknown` / `Newer than this build`, refreshed on window reload, slot create, and slot delete rather than every OnGUI frame.
- `DSMKeyRotationPanel` gates rotation behind `EditorUtility.DisplayDialog`, disables the button via `_rotating` until the awaited `UniTask` settles (reset in a `finally`), and reports success and failure — including `DSMRotationInterruptedException` — in a HelpBox plus a Console error carrying only the exception type and the runtime's value-free message.
- Seven EditMode cases cover envelope, legacy-flat, missing, corrupt, encrypted, wrong-key, and a byte-plus-mtime no-mutation assertion.
- Both panels are dedicated classes composed by the window; neither is declared inside `DSMManagerWindow.cs`.

## Task Commits

1. **Task 1: read-only inspector + version accessors** — `2dd654f` (feat)
2. **Tasks 2–3: version panel + key rotation panel** — `7241ad7` (feat)

## Files Created/Modified
- `Runtime/DSMSaveInspector.cs` — `TryReadOnDiskVersion(path, config, out version)` + extension-first encryption detection
- `Runtime/DSMSlotManager.cs` — `public int CurrentVersion => _migrationRunner.CurrentVersion`
- `Runtime/DSM.cs` — `public static int CurrentSaveVersion => Manager.CurrentVersion`
- `Editor/DSMSlotVersionPanel.cs` — cached per-slot readings + status rendering
- `Editor/DSMKeyRotationPanel.cs` — password field, confirm dialog, `_rotating` guard, result HelpBox
- `Editor/DSMManagerWindow.cs` — composes and draws both panels; refreshes readings on reload/create/delete
- `Editor/DSMManagerSlotOps.cs` — `ResolveSlotPath(slot)` (`.enc` when present, else `.json`)
- `Tests/Editor/DSMSaveInspectorTests.cs` — 7-case fixture

## Decisions Made
- **Legacy files report `true` + v1.** The plan left the contract open. Returning `true` says "this file was read and it is a v1 save", which is what makes the panel show `Needs migration: v1 → vN`; reserving `false` for genuinely unreadable files keeps the two states distinct.
- **Extension decides encryption, config is the fallback.** `config.Encrypt` alone would try to decrypt a leftover plain `.json` and report it unreadable.
- **The inspector's `catch (Exception)` is intentional and documented.** BUGS-01 forbids silent swallows; here the `false` return *is* the surfaced signal and the panel renders "Unknown" — no failure disappears.
- **Tests avoid the `DSM` static facade.** Touching `DSM.CurrentSaveVersion` in a test constructs the global manager against the project's real save directory (and replays any rotation journal). The encrypted-slot test compares against its own manager instead.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Tasks 2 and 3 committed together**
- **Found during:** Task 3 (rotation panel)
- **Issue:** Both panels are composed into `DSMManagerWindow.cs` in adjacent lines (field declarations and `OnGUI` draw calls). A Task-2-only commit would either omit the window wiring or reference a `DSMKeyRotationPanel` type that did not exist yet — an intermediate commit that does not compile.
- **Fix:** One commit containing both panels and their composition, with a message covering both.
- **Files modified:** Editor/DSMSlotVersionPanel.cs, Editor/DSMKeyRotationPanel.cs, Editor/DSMManagerWindow.cs, Editor/DSMManagerSlotOps.cs
- **Verification:** `dotnet build DSM.Editor.csproj -clp:ErrorsOnly` → 0 errors; both tasks' acceptance greps pass.
- **Committed in:** `7241ad7`

**2. [Rule 2 - Missing Critical] Added `DSMManagerSlotOps.ResolveSlotPath`**
- **Found during:** Task 2 (version panel)
- **Issue:** The panel needs a slot's file path, and the plan says to reuse the window's resolution — but path building lived inline inside the ops class's read/write methods, with nothing exposing it.
- **Fix:** Added `ResolveSlotPath(slot)` returning the `.enc` path when it exists, else `.json`, and passed it as the panel's `Func<string,string>`.
- **Files modified:** Editor/DSMManagerSlotOps.cs
- **Verification:** Editor assembly compiles; the panel resolves both encrypted and plain slots.
- **Committed in:** `7241ad7`

---

**Total deviations:** 2 auto-fixed (both missing-critical)
**Impact on plan:** No scope or contract change. Commit granularity and one small helper on the ops class; the panels, inspector, and guards are exactly as specified.

## Issues Encountered
- **`DSM.CurrentSaveVersion` builds the global manager in Edit Mode.** The plan specifies this accessor for the panel, and reading it lazily constructs `DSM`'s manager against the project's `Resources/DSMConfig` — which runs `RecoverInterruptedRotation()` and loads the default slot. It is only touched on refresh (not per frame), and the same happens for any Editor code calling into `DSM`, but the human check should confirm opening the Manager window does not produce surprising console output on a project with a stale rotation journal.
- Registered the four new files in the git-ignored generated csprojs (`DMS.Runtime.csproj`, `DSM.Editor.csproj`) so both assemblies actually compile-check. Unity regenerates these on import.

## User Setup Required
None - no external service configuration required.

## Verification Status

- **Automated (no Unity):** `dotnet build DMS.Runtime.csproj -clp:ErrorsOnly` → 0 errors; `dotnet build DSM.Editor.csproj -clp:ErrorsOnly` → 0 errors. Greps confirm `TryReadOnDiskVersion` exists with no `Migrate`/`Save`/`WriteJsonToDisk` call, both accessors exist, both panel classes live in their own files (not in `DSMManagerWindow.cs`), rotation is gated by `DisplayDialog` + `_rotating`, and `_newKey` appears in no log, dialog, or EditorPrefs.
- **Human (pending — Unity):** Run `DSMSaveInspectorTests` in the EditMode Test Runner (never batchmode). Then open **DSM ▸ Open Manager** with a mix of current and Phase-4-fixture stale slots: the Save Versions panel must flag the stale ones and leave every file byte-identical. Enter a new key and click **Rotate Encryption Key**: the confirmation must appear, the button must disable until the rotation finishes, slots must load with the new key afterwards, and the result HelpBox must show. Force a failure (e.g. rotate with encryption disabled) and confirm a visible error plus a Console error containing no key text. This is the EDIT-01 acceptance gate.

## Next Phase Readiness
- 05-05 can proceed: `DSMManagerSlotOps` now also exposes `ResolveSlotPath`, and the panels sit beside the state model the window-state tests drive.
- Reminder carried from 05-03: `DSMManagerSlotOps` is `internal` to `DSM.Editor`, and `DMS.Tests.Editor` does not reference `DSM.Editor` yet. Plan 05-05 must add that assembly reference plus internals access before its tests can compile.

---
*Phase: 05-performance-reactivity-editor-tooling*
*Completed: 2026-07-22*
