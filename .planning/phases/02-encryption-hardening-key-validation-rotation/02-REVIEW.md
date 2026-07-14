---
phase: 02-encryption-hardening-key-validation-rotation
reviewed: 2026-07-14T13:55:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - Runtime/DSM.cs
  - Runtime/DSMConfig.cs
  - Runtime/DSMEncryptionKey.cs
  - Runtime/DSMEncryptor.cs
  - Runtime/DSMSlot.cs
  - Runtime/DSMSlotManager.cs
  - Tests/Editor/DSMEncryptionKeyTests.cs
  - Tests/Editor/DSMEncryptorTests.cs
  - Tests/Editor/DSMKeyRotationTests.cs
  - Tests/Editor/DSMTestConfig.cs
findings:
  critical: 3
  warning: 4
  info: 1
  total: 8
status: issues_found
---

## Resolution (2026-07-15)

All 3 critical and 4 warning findings below have been fixed in `Runtime/DSMSlot.cs` and
`Runtime/DSMSlotManager.cs`:

- **CR-01** — rotation staging now uses a distinct `{slot}.enc.rotate.tmp` path
  (`DSMSlot.GetRotationTempPath`), and `DSMSlot.BeginRotation`/`EndRotation` suspend the
  autosave debounce path (`ScheduleSave`) for the duration of a rotation.
- **CR-02** — the COMMIT loop retries best-effort on failure; if slots remain uncommitted it
  throws the new `DSMRotationInterruptedException` telling the caller to restart, and leaves
  the journal in place for next-launch recovery.
- **CR-03** — `DSMEncryptor.Decrypt` now distinguishes a missing/wrong magic prefix
  ("unrecognized or pre-DSM2 legacy format") from a version mismatch and from genuine
  MAC/integrity failure, instead of one generic "corrupt or wrong key" message. No legacy
  fallback decoder was added (per PROJECT.md's breaking-change allowance) — the message just
  stops being misleading.
- **WR-01** — `StageReencryptAsync` now deletes its own `.rotate.tmp` if the post-write verify
  step throws.
- **WR-02** — `DSMSlotManager._rotationGate` rejects a second concurrent
  `RotateEncryptionKeyAsync` call with `InvalidOperationException`.
- **WR-03** — `RecoverInterruptedRotation` now catches per-slot and journal-read failures,
  logs a warning, and leaves the journal in place instead of throwing out of the constructor.
- **WR-04** — `Save()`/`SaveAsync()` now snapshot `_data` under `_dataLock`
  (`DSMSlot.SerializeSnapshot`) before serializing.

New regression tests added to `Tests/Editor/DSMKeyRotationTests.cs`:
`Rotate_ConcurrentAutosave_DoesNotCorruptOrThrow`, `Rotate_ConcurrentCall_SecondRejected`,
`Decrypt_LegacyFormat_ThrowsDistinguishableError`. `Rotate_JournalRecovery_...` updated for
the new temp filename.

Verified via `dotnet build` on `DMS.Runtime.csproj` and `DMS.Tests.Editor.csproj` (0 errors).
**Unity Test Runner confirmation is still open for a human** — batchmode execution is not run
by the agent in this project (see `.claude/CLAUDE.md`).

IN-01 (doc-comment accuracy) not addressed — informational only, no functional risk.

---

# Phase 02: Code Review Report

**Reviewed:** 2026-07-14T13:55:00Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Plan 02-01's Encrypt-then-MAC rewrite of `DSMEncryptor` is solid: MAC is verified in
constant time before any AES decryption, magic/version framing rejects malformed
buffers cleanly, and `DSMEncryptionKey.Validate` is genuinely wired in as the shared
chokepoint for `DSMConfig.SetEncryptionKey`, `DSMEncryptor.Encrypt`, and
`DSMEncryptor.Decrypt`. The unit tests for key validation and round-trip/tamper/
truncation/wrong-key behavior are well targeted.

Plan 02-02's key-rotation feature is where the real problems live. The stage-then-
commit design is correct in outline, but the staging temp file reuses the *exact
same path* (`{slot}.enc.tmp`) that ordinary `Save()`/`SaveAsync()` also use for its
own atomic-write temp file. Because staging and normal saves are only mutually
exclusive within a single slot's own `_ioGate` acquisition (not across the whole
rotation), a debounced autosave firing on a slot between its `StageReencryptAsync`
and `CommitReencrypt` calls can silently clobber the staged re-encrypted file with
freshly-written old-key data, or delete the very file `CommitReencrypt` is about to
rename. That turns the documented "no slot is ever left unreadable" guarantee into a
guarantee that does not actually hold under realistic AutoSave-enabled usage. A
second, unrelated but equally serious issue: encrypted saves written before this
phase (no magic/version/MAC framing) can never be decrypted by the new `Decrypt`
implementation — they are unconditionally reported as "corrupt," which is a silent
data-loss trap for any existing installed base with encrypted saves under the old
format. See Critical Issues below.

## Critical Issues

### CR-01: Rotation staging reuses the same temp filename as normal Save/SaveAsync, allowing a concurrent autosave to corrupt an in-progress key rotation

**File:** `Runtime/DSMSlot.cs:90` (`Save`), `Runtime/DSMSlot.cs:158` (`SaveAsync`), `Runtime/DSMSlot.cs:241` (`StageReencryptAsync`), `Runtime/DSMSlot.cs:264-277` (`CommitReencrypt`)

**Issue:**
`GetSavePath()` returns `{saveDirectory}/{slotName}.enc`, and both the ordinary
save path and the rotation staging path derive the *same* temp file from it:

```csharp
// Save() / SaveAsync()
var path = GetSavePath();
var tmpPath = path + ".tmp";              // {slot}.enc.tmp

// StageReencryptAsync()
var encPath = GetSavePath();
var tmpPath = encPath + ".tmp";           // {slot}.enc.tmp  <-- same path
```

`StageReencryptAsync` acquires `_ioGate`, writes/verifies `{slot}.enc.tmp`, and
*releases* `_ioGate` before returning to `DSMSlotManager.RotateEncryptionKeyAsync`.
`CommitReencrypt` (which renames that same `.tmp` into `.enc`) is only called later,
after every slot in the batch has been staged and after the rotation journal is
written (`Runtime/DSMSlotManager.cs:111-134`). Between those two points, the gate for
that specific slot is free, and `_config.EncryptionKey` still holds the **old** key
(it isn't swapped until `Runtime/DSMSlotManager.cs:137`, after the whole commit
burst succeeds). If AutoSave is enabled (the default — `DSMConfig._autoSave = true`)
and a `Set()` call on that same slot has a debounced save pending or fires during
this window, `SaveAsync()` will:

1. Acquire the now-free `_ioGate`.
2. Serialize current data, encrypt it with the **old** key (config key hasn't
   rotated yet).
3. Write it to the *same* `{slot}.enc.tmp` path, overwriting the staged
   new-key ciphertext.
4. Call `ReplaceFile(tmpPath, path)`, which **renames the .tmp away immediately**
   (`File.Move`/`File.Replace`), so the file no longer exists at that path at all.

When `CommitReencrypt()` for that slot subsequently runs, `ReplaceFile(tmpPath,
encPath)` finds `destPath` (`.enc`) already exists (from the interleaved autosave)
and calls `File.Replace(tmpPath, destPath, null)` — but `tmpPath` no longer exists,
so this throws. That exception escapes the COMMIT loop in
`RotateEncryptionKeyAsync` (which has no try/catch around it, see CR-02), after the
rotation journal has already been written and potentially after other slots have
already been committed to the new key while `_config.EncryptionKey` is still the old
key. Even in the less-dramatic case where the autosave finishes and
`CommitReencrypt` never observes a missing file (e.g., ordering happens to avoid the
FileNotFoundException), the net effect is that the slot silently reverts to
old-key ciphertext right before/after being "successfully" rotated — permanently
inconsistent with the rest of the batch and with the new config key.

This is a genuine, reachable data-corruption/crash path in the single most safety-
critical feature this phase adds (atomic key rotation), and it is not covered by any
existing test — `DSMKeyRotationTests` never exercises a concurrent `Set()`/AutoSave
during rotation.

**Fix:** Give rotation staging its own, non-colliding temp filename (e.g.
`{slot}.enc.rotate.tmp`) so it can never be clobbered or consumed by an unrelated
`Save()`/`SaveAsync()` call, and update `CommitReencrypt`, `CleanupStagedTemp`, and
`RecoverInterruptedRotation` to use the same distinct name:

```csharp
private string GetRotationTempPath() => GetSavePath() + ".rotate.tmp";
```

This alone does not make rotation immune to AutoSave writing a fresh old-key `.enc`
file for that slot mid-rotation (see CR-02's related note), but it removes the file-
level collision that currently causes crashes/corruption. For full correctness,
`RotateEncryptionKeyAsync` should also suppress/defer AutoSave for the duration of
the whole rotation (e.g. a manager-level flag that `ScheduleSave()` checks) so that
no slot's on-disk content can be written with the stale key while rotation is in
flight.

---

### CR-02: A failure partway through the COMMIT burst leaves the running process in an unreadable state with no in-session recovery

**File:** `Runtime/DSMSlotManager.cs:130-139`

**Issue:**

```csharp
WriteRotationJournal(dir, encryptedSlotNames);

// COMMIT — rename every staged .tmp into place.
foreach (var slot in stagedSlots)
    slot.CommitReencrypt();          // <-- no try/catch

// Commit the new key LAST, only after every slot commit succeeded.
_config.SetEncryptionKey(newKey);

DeleteRotationJournal(dir);
```

If `CommitReencrypt()` throws for any slot partway through this loop (see CR-01 for
one concrete trigger, but disk-full/permission errors are equally possible), the
exception propagates straight out of `RotateEncryptionKeyAsync` uncaught. At that
point:

- Some slots earlier in `stagedSlots` are already renamed to new-key ciphertext.
- `_config.SetEncryptionKey(newKey)` was never reached, so `_config.EncryptionKey`
  is still the **old** key.
- The rotation journal is left on disk (good for the *next* app launch via
  `RecoverInterruptedRotation`), but **for the remainder of the current process**,
  any `Load()`/`Save()` on an already-committed slot will fail to decrypt (wrong
  key) or silently re-encrypt with the wrong key, and there is no code path that
  retries or completes the commit within the same session.

The class-level doc comment on `RotateEncryptionKeyAsync`
(`Runtime/DSMSlotManager.cs:78-85`) and the facade doc comment on
`DSM.RotateEncryptionKeyAsync` (`Runtime/DSM.cs:70-77`) both claim "no slot is ever
left unreadable" — that claim only holds across a *process restart*, not within the
process that experienced the interrupted commit. A caller that catches the thrown
exception and keeps running (which is the natural thing to do — nothing in the API
signals "the app must now restart") will be serving reads/writes against a broken
key/file state indefinitely.

**Fix:** At minimum, catch failures in the COMMIT loop and immediately attempt to
finish committing the remaining slots (best-effort), and/or immediately call the
same recovery logic `RecoverInterruptedRotation` uses so the in-memory state is
reconciled without requiring a restart. If recovery genuinely cannot be completed
in-process, surface a distinct exception type (e.g. `DSMRotationInterruptedException`)
that clearly documents "the application must restart before further save/load calls
on any slot will work," rather than letting a generic I/O exception bubble up
looking like an ordinary transient failure.

---

### CR-03: Encrypted saves written before this phase can never be read again — silently reported as "corrupt" instead of a version mismatch

**File:** `Runtime/DSMEncryptor.cs:19-20`, `Runtime/DSMEncryptor.cs:92-101`

**Issue:** The previous `DSMEncryptor.Decrypt` implementation (see the diff base)
used a fixed `[16-byte IV][32-byte salt][ciphertext]` layout with no magic bytes, no
format version, and no MAC. The new implementation unconditionally requires a 4-byte
`"DSM2"` magic prefix and version byte:

```csharp
for (var i = 0; i < MagicBytes.Length; i++)
{
    if (data[i] != MagicBytes[i])
        throw new DSMEncryptionException(
            "DSM: encrypted save failed integrity verification — file is corrupt, truncated, or the key is wrong.");
}
```

Any `.enc` file produced by the pre-phase-02 encryptor will have its first bytes be
part of the old IV, essentially never matching `"DSM2"`. Decrypting such a file with
the *correct* key now unconditionally throws `DSMEncryptionException` with a message
that says the data is "corrupt... or the key is wrong" — which is misleading (the
key is correct; the format is simply unrecognized) and, more importantly, there is
**no fallback path anywhere in this file** that attempts the legacy layout. For any
existing installation that already has encrypted save data on disk, upgrading to
this phase's code makes that data permanently and silently unreadable through the
public API. This satisfies the "data loss risk" criterion for a Critical finding
regardless of whether it exists as a temporary rotation artifact.

**Fix:** Either (a) add a legacy-format fallback decrypt path (detect the absence of
a recognized magic prefix and attempt the old fixed `[IV][salt][ciphertext]` layout
before giving up), transparently re-writing the file in the new format on next save,
or (b) if a breaking format bump is an accepted/intentional tradeoff for this phase,
make that explicit: emit a distinguishable exception/log message ("save file uses an
unsupported legacy format and cannot be read; delete or migrate it manually") instead
of the generic integrity-failure message, and document the breaking change
prominently (release notes / migration guide) so integrators aren't surprised by
silent player data loss.

## Warnings

### WR-01: `StageReencryptAsync` does not clean up its own temp file if the post-write verify step fails

**File:** `Runtime/DSMSlot.cs:235-257`

**Issue:** The catch block in `RotateEncryptionKeyAsync`
(`Runtime/DSMSlotManager.cs:121-126`) only cleans up temp files for slots that
already succeeded and were added to `stagedSlots`:

```csharp
catch
{
    foreach (var slot in stagedSlots)
        slot.CleanupStagedTemp();
    throw;
}
```

`stagedSlots.Add(slot)` happens only *after* `StageReencryptAsync` returns
successfully (`Runtime/DSMSlotManager.cs:116-118`). If the currently-staging slot's
own call throws *after* it has already written `tmpPath` — which happens for the
verify step at the end of `StageReencryptAsync`:

```csharp
await File.WriteAllBytesAsync(tmpPath, reencrypted);
var verifyBytes = await File.ReadAllBytesAsync(tmpPath);
DSMEncryptor.Decrypt(verifyBytes, newKey);   // <-- if this throws, tmpPath is already on disk
```

— that slot is never added to `stagedSlots`, so its leftover `.tmp` is never
cleaned up by the caller's catch block. This directly contradicts the "no leftover
`.tmp` staged files" invariant asserted by
`DSMKeyRotationTests.Rotate_PreCommitFailure_LeavesAllSlotsOnOldKey`, which only
happens to pass because its corrupt-slot scenario fails at the *first* decrypt (with
`oldKey`), before any `.tmp` is ever written — the verify-failure path is untested.

**Fix:** Wrap the write+verify portion of `StageReencryptAsync` in its own
try/catch that deletes `tmpPath` on any failure before rethrowing:

```csharp
await File.WriteAllBytesAsync(tmpPath, reencrypted);
try
{
    var verifyBytes = await File.ReadAllBytesAsync(tmpPath);
    DSMEncryptor.Decrypt(verifyBytes, newKey);
}
catch
{
    if (File.Exists(tmpPath)) File.Delete(tmpPath);
    throw;
}
```

---

### WR-02: No mutual exclusion against concurrent/re-entrant `RotateEncryptionKeyAsync` calls

**File:** `Runtime/DSMSlotManager.cs:86-140`

**Issue:** `RotateEncryptionKeyAsync` reads `_config.EncryptionKey` once at the top
as `oldKey` and stages/commits against that snapshot, but nothing prevents a second
concurrent call (e.g. a caller awaiting two rotations via `UniTask.WhenAll`, or a
double-tap on a "rotate key" UI button before the first `await` resolves) from
running at the same time. The two calls would race on `GetAllSlots()`, on each
slot's `_ioGate` (which only serializes per-slot, not per-rotation), and on
`_config.SetEncryptionKey` — with no defined outcome and no guard rejecting the
second call outright.

**Fix:** Add a manager-level `SemaphoreSlim _rotationGate = new(1, 1)` (or an
`Interlocked`-guarded boolean) around the whole method body, and throw
`InvalidOperationException("DSM: a key rotation is already in progress.")` if a
second call arrives while one is in flight.

---

### WR-03: `RecoverInterruptedRotation` has no error handling — any failure bricks `DSMSlotManager` construction entirely

**File:** `Runtime/DSMSlotManager.cs:148-164`

**Issue:** This method is called unconditionally from the constructor
(`Runtime/DSMSlotManager.cs:31`), before anything else runs, and has no try/catch:

```csharp
public DSMSlotManager(DSMConfig config)
{
    _config = config;
    RecoverInterruptedRotation();   // unguarded
    _activeSlot = GetOrCreateSlot(config.DefaultSlot);
    _activeSlot.Load();
}
```

If the journal file is malformed (e.g. contains a slot name that
`DSMSlotNameValidator.Validate` rejects — a plausible outcome of manual tampering,
disk corruption, or a future format change), or `CommitReencrypt()` fails for any
reason (locked file, permission error, disk full) while replaying a pending rename,
the exception propagates straight out of the constructor. Since `DSM.Initialize()`
(`Runtime/DSM.cs:88-96`) calls `new DSMSlotManager(config)` directly with no
try/catch, this takes down the *entire* DSM facade for the app session — not just
the one slot involved in the interrupted rotation. Given this code path only runs
after an already-abnormal event (an interrupted rotation), it is exactly the
scenario most likely to also see other kinds of partial/corrupt state, making this a
realistic single point of total failure.

**Fix:** Wrap each per-slot recovery iteration (and the journal read itself) in
try/catch, log a warning, and leave the journal in place (skip deleting it) if any
slot's recovery fails, so a future launch/manual intervention can retry rather than
losing the ability to construct `DSMSlotManager` at all.

---

### WR-04: `Save()`/`SaveAsync()` serialize `_data` without holding `_dataLock`

**File:** `Runtime/DSMSlot.cs:88` (`Save`), `Runtime/DSMSlot.cs:156` (`SaveAsync`)

**Issue:** `Set<T>`, `Delete`, and `Clear` all mutate `_data` under `_dataLock`, but
`Save()`/`SaveAsync()` read `_data` for serialization without acquiring that lock:

```csharp
var json = _serializer.Serialize(_data, _config.PrettyPrint);
```

If a `Set()` call runs on another thread concurrently with `Save()`/`SaveAsync()`
enumerating `_data` inside `_serializer.Serialize`, this is a `Dictionary<TKey,
TValue>` mutated-during-enumeration race that can throw
`InvalidOperationException: Collection was modified` mid-serialization — which, for
an encrypted save, means the write is aborted after `GetSavePath()`/before the
temp-file write, but the surrounding try/catch in `Save()`/`SaveAsync()` only guards
the file I/O portion, not the serialization call, so this exception propagates
without any temp-file cleanup concern (no `.tmp` was created yet) but does abort the
save outright. This predates this phase but remains present in the reviewed file and
is directly adjacent to the encryption I/O path this phase hardened.

**Fix:** Snapshot `_data` under `_dataLock` before serializing:

```csharp
Dictionary<string, JToken> snapshot;
lock (_dataLock) { snapshot = new Dictionary<string, JToken>(_data); }
var json = _serializer.Serialize(snapshot, _config.PrettyPrint);
```

## Info

### IN-01: `DSMEncryptionKey`'s doc comment overstates Editor tooling's use of `Validate`

**File:** `Runtime/DSMEncryptionKey.cs:6-9`

**Issue:** The class doc comment states: "Both the runtime path... and Editor
tooling (`DSMConfigEditor`, `DSMManagerWindow`) must call `Validate` before using a
key." Neither `Editor/DSMConfigEditor.cs` nor `Editor/DSMManagerWindow.cs` calls
`DSMEncryptionKey.Validate` or `DSMConfig.SetEncryptionKey` directly — they read
`config.EncryptionKey` and pass it straight to `DSMEncryptor.Encrypt`/`Decrypt`,
which happen to validate internally, so behavior is currently safe, but the comment
asserts a design invariant ("Editor tooling... must call Validate") that isn't
actually implemented in those files. Neither file is in this phase's change set, so
this is a documentation-accuracy note rather than a functional defect.

**Fix:** Either update the doc comment to describe the *actual* chokepoint (every
call ultimately routes through `DSMEncryptor`'s internal `Validate` call) or, if the
intent really is for Editor tooling to validate explicitly (e.g. to fail fast with a
clearer UI message before attempting a test encrypt/decrypt), add the `Validate`
calls to `DSMConfigEditor.TestEncryption()` and `DSMManagerWindow`'s encrypt/decrypt
call sites.

---

_Reviewed: 2026-07-14T13:55:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
