# Pitfalls Research

**Domain:** Unity save-system hardening (encryption key validation/rotation, thread-safety retrofit, save versioning/migration, Editor window refactor, test-suite bootstrapping)
**Researched:** 2026-07-08
**Confidence:** MEDIUM-HIGH (synthesized from verified Unity/IMGUI/UniTask engineering constraints + established encryption/migration engineering practice; DSM-specific claims cross-referenced against `.planning/codebase/CONCERNS.md`)

This file goes **one level deeper** than CONCERNS.md: it does not repeat "what's broken," it covers **how teams typically botch the fix** when hardening exactly these five areas.

---

## Critical Pitfalls

### Pitfall 1: Validating the key at the wrong layer, so enforcement is inconsistent

**What goes wrong:**
Key validation gets added to `DSMSlot.Save()`/`Load()` (the fix approach CONCERNS.md suggests) but the Editor window has its own independent key-retrieval path (`DSMManagerWindow.cs` line 206) that still falls back to an empty string. Result: runtime enforces the rule, the Editor tool that inspects/edits save data silently bypasses it — two code paths, one source of truth violated.

**Why it happens:**
The empty-key fallback exists in at least 4 places (`DSMSlot.cs` lines 59/74/87, `DSMManagerWindow.cs` line 206). Teams fix the ones flagged in the audit and miss the ones discovered mid-implementation, because validation logic is duplicated rather than centralized.

**How to avoid:**
Centralize key resolution and validation in a single method (e.g., `DSMConfig.GetValidatedEncryptionKey()` or a `HasEncryptionKey` property that all call sites — runtime and Editor — must go through). Grep every occurrence of `EncryptionKey` before considering the phase done; there should be exactly one place that decides "key is usable."

**Warning signs:**
`grep -rn "EncryptionKey" Runtime/ Editor/` returns more than one place that reads the raw field instead of calling a shared accessor.

**Phase to address:**
Encryption/security hardening phase (the phase covering "Bugs & Security" requirements).

---

### Pitfall 2: Validating key *length* instead of key *strength*, giving false security confidence

**What goes wrong:**
"Reject keys shorter than 16 chars" (the fix approach in CONCERNS.md) stops the empty-key bug but does nothing to stop a 16-character low-entropy key (`"1111111111111111"`) from passing validation and being fed into PBKDF2. The team ships "key validation" as a checkbox and considers the encryption hardened, when the actual cryptographic weakness (weak passphrase → weak derived key) is untouched.

**Why it happens:**
Length checks are trivial to implement and test; entropy/strength checks are not, so they get scoped out silently. The requirement "reject empty/short keys" reads as sufficient when it's actually a minimum bar.

**How to avoid:**
Treat length validation as necessary-but-not-sufficient. Document explicitly (in code comments and README) that DSM validates *presence and minimum length* only — it is the host game's responsibility to supply a high-entropy key. Don't let the phase's Definition of Done imply "encryption is now secure"; scope it as "encryption no longer silently fails."

**Warning signs:**
Test suite only asserts `SetEncryptionKey("")` throws and `SetEncryptionKey("shortkey")` throws — no test documents what a *valid* key should look like or that DSM makes no strength guarantee beyond length.

**Phase to address:**
Encryption/security hardening phase. Document as a known limitation, not a silent gap.

---

### Pitfall 3: Key rotation implemented as a non-atomic overwrite, risking total data loss on failure

**What goes wrong:**
The natural implementation of "rotate key" is: load with old key → decrypt → re-encrypt with new key → write to the same file path. If the process is interrupted (exception, domain reload, crash) between decrypt and the final write, or if the write itself fails partway, the save file is left corrupted or truncated — and because the old key is already gone from memory (overwritten in `DSMConfig`), the data is now unrecoverable with either key. Public technical literature on key rotation calls this the "exposure window between decryption using the old key and re-encryption using the new key" — corruption or interruption inside that window is unrecoverable by design unless guarded against.

**Why it happens:**
Rotation is treated as "just call Save() again with a different key" rather than as a distinct, riskier operation that needs its own transactional guarantee. `DSMSlot.Save()` likely writes directly to the target path today (no atomic write pattern visible in the current code), so reusing it for rotation inherits the same non-atomicity.

**How to avoid:**
- Never overwrite the config's key field until the rotation has fully succeeded and been verified.
- Write the re-encrypted file to a temp path, verify it round-trips (decrypt-and-compare or checksum), then atomically move/replace the original (`File.Replace` or write-then-rename).
- Keep the old key resident in memory for the duration of the rotation operation; only discard it after the new file is confirmed durable.
- Consider writing a `.bak` of the pre-rotation file that is only deleted after successful verification.

**Warning signs:**
Rotation code path calls `Save()` on the same file path with no temp file, no backup, and no post-write verification step. No test simulates "exception thrown mid-rotation" and asserts the original file is still intact and readable with the old key.

**Phase to address:**
Missing Critical Features phase (key rotation). This should be scoped as its own sub-task with explicit atomicity requirements, not bundled as "reuse Save()."

---

### Pitfall 4: Fixing the `_data` race but leaving the debounce-timer race unsynchronized

**What goes wrong:**
Thread-safety work focuses on the obviously-named target (`DSMSlot._data`, per CONCERNS.md) — wrapping it in a `lock` or swapping to `ConcurrentDictionary` — while the *other* known race in the same class (the `CancellationTokenSource` cancel/dispose pattern in `ScheduleSave()`, lines 136-156) is left untouched because it wasn't the dictionary. Teams ship "thread-safety added" having fixed one race and left a second, already-documented one (`ObjectDisposedException` on rapid `ScheduleSave()` calls) in place.

**Why it happens:**
"Thread safety" gets scoped narrowly to the data structure named in the ticket instead of to all shared mutable state touched by concurrent call paths. CTS lifecycle bugs don't look like classic "thread safety" bugs (they can surface even single-threaded, via async re-entrancy), so they get filed as a separate, lower-priority fix.

**How to avoid:**
Before starting, enumerate *every* piece of mutable state `DSMSlot` owns that's touched by more than one entry point (`Set`, `Get`, `Load`, `Save`, `ScheduleSave`, `DebouncedSaveAsync`) — not just `_data`. Fix CTS lifecycle (single active debounce token, cancel-check-before-use, dispose only after confirmed unreferenced) in the same phase as the dictionary fix, since both stem from the same root cause: unsynchronized shared state under async re-entrancy.

**Warning signs:**
PR/diff for "thread-safety" phase touches only `_data`-adjacent lines and doesn't touch `ScheduleSave()`/`DebouncedSaveAsync()` at all.

**Phase to address:**
Fragile Areas / thread-safety phase — scope explicitly includes both `_data` synchronization and debounce CTS lifecycle as one unit of work.

---

### Pitfall 5: Treating a Unity async re-entrancy bug as a classic multi-threading bug

**What goes wrong:**
The team reaches for `lock`/`Monitor`/`ConcurrentDictionary` — the standard toolkit for OS-thread races — when the actual race in a UniTask-based Unity codebase is usually **async re-entrancy on the main thread**: `Set()` is called synchronously while an `await SaveAsync()` on the same slot is suspended mid-flight, both running on the Unity main thread but interleaved by the async state machine. A `lock` around synchronous code works, but `lock` cannot legally straddle an `await` boundary in C#; teams work around this by using `SemaphoreSlim.WaitAsync()`/`Release()` and then forget the `Release()` in a `finally`, turning "fix the race" into "introduce a deadlock on the first thrown exception."

**Why it happens:**
"Thread safety" is a familiar label that pattern-matches to familiar tools, even when the actual hazard (async re-entrancy, not OS-level concurrency) calls for a different mental model and different tools (`SemaphoreSlim`, re-entrancy guards, or simply queuing operations per-slot).

**How to avoid:**
Identify whether the hazard is true multi-threading (e.g., a background thread calling into DSM) or async re-entrancy (single thread, interleaved async continuations). For DSM, most call sites are main-thread + UniTask, so the fix is usually: (a) a lightweight non-reentrant guard/queue per slot for async operations, plus (b) a simple `lock` only around the strictly synchronous dictionary access in `Set`/`Get`. If `SemaphoreSlim` is used for the async path, always release in `finally`, and add a test that throws mid-operation and confirms the semaphore is released (no permanent deadlock).

**Warning signs:**
Code uses `SemaphoreSlim.WaitAsync()` without a matching `try/finally { Release() }`. Tests never simulate an exception during a locked/semaphore-held async operation.

**Phase to address:**
Thread-safety phase — explicitly distinguish "true concurrency" vs "async re-entrancy" hazards during discuss/plan, since they need different fixes.

---

### Pitfall 6: "No exception thrown" concurrency tests that don't catch lost writes

**What goes wrong:**
The team writes a concurrency test that fires N `Set()`/`SaveAsync()` calls in parallel and asserts "no exception was thrown," declares thread-safety verified, and moves on. Meanwhile the actual failure mode of a race isn't always an exception — it's often a **lost update** (last-write-wins silently discards a concurrent write) or a torn read. A test that only checks "didn't crash" can pass 100% of the time while silently losing data every run.

**Why it happens:**
"No exception" is the easy, obvious assertion. Asserting final-state correctness after concurrent operations requires more careful test design (deterministic interleaving or statistical repetition with value verification), which teams skip under time pressure — especially when this is the *first* time tests are being written for this codebase (see Pitfall 11).

**How to avoid:**
For every concurrency test, assert the **final data state** matches an expected outcome (e.g., "after N concurrent `Set()` calls with distinct keys, all N keys are present with correct values" or "after concurrent `Set()` + `SaveAsync()`, the loaded file matches the in-memory state at a well-defined point"). Run concurrency tests multiple times (races are often intermittent) or use deterministic scheduling (manual `UniTask` continuation stepping) rather than relying on wall-clock parallelism alone.

**Warning signs:**
Test method bodies contain `Assert.DoesNotThrow(...)` as the only assertion for a concurrency scenario, with no assertion on the resulting dictionary/file contents.

**Phase to address:**
Test Coverage phase, in coordination with the thread-safety phase — write the failing test *before* the fix (see Pitfall 12).

---

### Pitfall 7: Global slot-level version field can't express partial/per-key migrations

**What goes wrong:**
CONCERNS.md's suggested fix approach is a single `SaveVersion` field on `DSMConfig`, checked at load time. This works for "everything changed shape" migrations but breaks down the moment a real-world change is smaller: one `DSMConstant` key gets renamed, or one default value changes, while everything else in the slot is untouched. A single global version number forces an all-or-nothing migration function that must know how to reconstruct the *entire* slot from scratch for every version jump, even when 95% of keys didn't change — this balloons migration code complexity fast and teams typically discover the granularity mismatch only after the second or third schema change, by which point the versioning scheme is baked into shipped saves and hard to redesign without another breaking change.

**Why it happens:**
A single version int is the simplest thing to implement first, and it satisfies the *first* migration scenario tested. The granularity problem only appears on the second real schema change, which is often after the hardening milestone has already closed.

**How to avoid:**
Design the migration callback interface around **transform functions keyed by (fromVersion, toVersion)** that operate on the raw key/value bag, not a full-slot reconstruction. Even with one global slot version, keep individual migration steps small and composable (`v1→v2`, `v2→v3`, chained) rather than one big `if (version == 1) { ...rebuild everything... }` block. This keeps the door open for finer-grained versioning later without changing the migration contract.

**Warning signs:**
Migration interface signature takes the entire old data blob and returns an entirely new blob in one function, with no per-key/per-step decomposition. Second migration ever written duplicates 90% of the logic from the first.

**Phase to address:**
Missing Critical Features phase (schema/versioning). Flag as needing a design pass before implementation, not just direct coding from the CONCERNS.md fix approach.

---

### Pitfall 8: Migration written and tested once, then silently rotted with no regression suite

**What goes wrong:**
Migration logic gets built, manually verified against one hand-crafted "old-format" save file, and shipped. Because there is no golden-file regression test, the next schema change (or a refactor of the save-loading path) can silently break the v1→v2 migration path with nobody noticing — the break only surfaces when an actual user's old save fails to load, at which point the original "old-format" sample file used during development is long gone.

**Why it happens:**
Migration code is exercised rarely (once per user, once per version bump) so it doesn't get the continuous regression coverage that hot paths get. Since backward-compat isn't a hard requirement for *this* milestone, it's tempting to treat migration as throwaway/one-off code rather than a permanent contract.

**How to avoid:**
Commit fixture files for every schema version the migration system claims to support (e.g., `TestFixtures/save-v1.json`, `save-v2.json`) and write a regression test that loads each fixture through the current migration pipeline and asserts the final in-memory state is correct. Treat these fixtures as append-only — never delete an old one, even after backward-compat for that specific version is dropped, because the migration *code path* for other still-supported versions can still regress.

**Warning signs:**
No `TestFixtures/`-style directory of versioned sample save files exists. Migration tests construct their "old" input data inline/dynamically rather than loading a static fixture that mirrors a real historical file format.

**Phase to address:**
Test Coverage phase, coordinated with Missing Critical Features (versioning) phase — fixtures should be captured *at the time* each schema version is finalized, not retroactively.

---

### Pitfall 9: Migration silently substitutes default values instead of remapping renamed keys, discarding user data

**What goes wrong:**
The simplest way to "migrate" a renamed key is to do nothing: the old key is gone, the new key doesn't exist in the old file, so `Get<T>()` on the new key just returns the `DSMConstant` default. This *looks* like migration succeeded (no exception, app runs fine) but it's actually silent data loss — the user's actual prior value is discarded and replaced with a default, and nothing in logs or UI indicates this happened.

**Why it happens:**
Default-value fallback is already a built-in DSM behavior (existing `Get<T>()` semantics), so it's the path of least resistance — "migration" ends up being "let the existing default-fallback mechanism paper over the missing key" rather than an explicit remap step. It's easy to convince yourself this is fine ("the user gets a sane default") without recognizing it's silently destructive for renamed (not new) keys.

**How to avoid:**
Migration callbacks must explicitly distinguish "key is new in this version" (default fallback is correct) from "key was renamed/restructured" (requires an explicit old-key → new-key value copy). Require migration authors to enumerate renamed/removed keys explicitly rather than relying on ambient default behavior. Log at INFO/WARN level whenever a migration step actually transforms or discards data, so it's auditable.

**Warning signs:**
Migration "implementation" for a renamed key is simply the absence of any code — the old key stays unread and the new key uses its default. No log line fires when a slot is migrated.

**Phase to address:**
Missing Critical Features phase (versioning/migration) — should be an explicit design requirement in the phase's SPEC, not an emergent behavior.

---

### Pitfall 10: Extracting `DSMManagerWindow` into multiple classes but keeping GUI-order-dependent code split across them

**What goes wrong:**
IMGUI is stateless per call but relies on **identical control call order across the Layout event and the Repaint event** within a single `OnGUI()` invocation cycle. When `DSMManagerWindow.cs` (825 lines) is split into separate classes (e.g., a rendering class, a slot-controller class) and the new classes make control-drawing decisions based on state that can differ between the Layout and Repaint passes (e.g., a field mutated by the slot-controller mid-draw, or an early return added "for clarity" during the split), Unity throws `ArgumentException: Getting control X's position in a group with only Y controls when doing repaint` — a bug that often doesn't reproduce until a specific slot count/foldout state combination is hit, well after the refactor is merged.

**Why it happens:**
Splitting a monolithic `OnGUI` into helper methods/classes is a natural, low-risk-*looking* refactor (extract method), but IMGUI's implicit ordering contract isn't visible in the code — nothing signals "this call must run identically on every event type." Refactoring tools and code review don't catch this because the code compiles fine and *usually* runs fine; it only breaks under specific state transitions.

**How to avoid:**
When extracting rendering code, keep the same GUI calls in the same order for both Layout and Repaint — never gate a `GUILayout`/`EditorGUILayout` call behind state that can legitimately differ between the two passes within one `OnGUI` cycle (e.g., don't recompute "is this slot still selected" mid-draw if a callback in the same frame could have changed it). Prefer separating **pure logic** (reflection scan results, slot list, validation) from **rendering** cleanly, so rendering classes only ever read already-stable data computed before `OnGUI` calls begin, rather than mutating state as they draw.

**Warning signs:**
`ArgumentException` mentioning "control ID" or "group with only N controls" appearing in the Editor console, especially after switching slots or triggering a delete/refresh mid-draw. Rendering code contains `if (someCachedField) { ...draw fewer controls... }` where `someCachedField` can be mutated by an event handler in the same class.

**Phase to address:**
Tech Debt & Performance phase (Editor window split) — call out IMGUI ordering as an explicit review checklist item for this phase, not just "split into smaller files."

---

### Pitfall 11: Refactoring and bug-fixing in the same commits, making regressions unattributable

**What goes wrong:**
The plan bundles "split `DSMManagerWindow.cs`" together with "fix broad catch," "add key validation," and "cache reflection scan" as one continuous pass (they're natural to do together since you're already in the file). When something regresses after this phase — e.g., slot switching behaves differently — there's no way to tell from git history whether it's the structural split, the exception-handling change, or the caching change that caused it, because they're interleaved in the same commits/diffs.

**Why it happens:**
It feels inefficient to touch the same 825-line file multiple times across separate phases. Combining "while I'm in here" fixes with structural refactor is a natural instinct but destroys bisectability.

**How to avoid:**
Sequence the work: (1) pure structural refactor first (extract classes, no behavior change — verifiable by manual smoke test or, better, characterization tests captured beforehand), committed and verified in isolation; (2) behavior fixes (exception handling, key validation, caching) as separate, smaller commits on top of the now-split structure. This also makes each commit's diff reviewable against a single concern.

**Warning signs:**
A single PR/commit touches exception-handling logic, caching logic, and file-splitting/class-extraction in the same diff hunks.

**Phase to address:**
Applies across Bugs & Security phase and Tech Debt & Performance (Editor split) phase — sequence these as separate phases/plans rather than one combined pass, even though they touch the same file.

---

### Pitfall 12: Writing tests *after* fixes instead of characterizing current behavior first, especially for code with no test coverage

**What goes wrong:**
Since DSM currently has zero automated tests, the team's instinct is to implement all the fixes (validation, thread-safety, versioning, refactor) and *then* add tests to "cover" the changes. This means the tests are designed around the *already-fixed* code's expected behavior, so they can't prove the fix actually resolved the original bug (there's no "red" test that failed against the old code) — and worse, there's no safety net *during* the refactor/fix work itself, so regressions introduced mid-fix aren't caught until manual QA, if at all.

**Why it happens:**
"Add tests" is listed as its own separate Active requirement group in PROJECT.md, which subtly encourages treating it as a phase that comes *after* the fixes are done, rather than as the mechanism that drives and verifies each fix.

**How to avoid:**
For each specific bug/gap already identified (encryption with empty key, concurrent `Set`+`Load`, malformed JSON, slot switch-while-selected), write the test *first*, confirm it fails against current behavior (red), then implement the fix until it passes (green). This is especially valuable here because the bugs are already enumerated in CONCERNS.md — there's no discovery cost, only the discipline of test-first ordering.

**Warning signs:**
Test files are added in the same commit as (or after) the corresponding fix, with no evidence the test was ever run against pre-fix code.

**Phase to address:**
Every phase that touches a CONCERNS.md-flagged bug should include its regression test as part of that phase's definition of done, rather than deferring all testing to a single later "Test Coverage" phase.

---

### Pitfall 13: Blocking/deadlocking UniTask-based async tests in Edit Mode

**What goes wrong:**
Tests for `DSMSlot.SaveAsync()`/`LoadAsync()` (UniTask-based) get written as synchronous Edit Mode tests using `.GetAwaiter().GetResult()` or `.Result`-style blocking calls to "just get the value out." In Edit Mode (no player loop actively pumping continuations the way Play Mode does), this can hang indefinitely instead of failing fast, and CI runs stall or time out rather than reporting a clean failure — a well-documented class of bug with blocking on incomplete awaitables, and UniTask specifically disallows awaiting/consuming certain awaiters more than once, so ad hoc blocking patterns can also throw obscure exceptions unrelated to the actual test assertion.

**Why it happens:**
It's the path of least resistance to writing a synchronous `[Test]` when the surrounding test framework/tutorial the author is used to (NUnit `[Test]`) doesn't natively support `async` easily, so the temptation is to force synchronous access rather than switching to `[UnityTest]` + `IEnumerator` (`.ToCoroutine()`) or an `async Task`-returning `[Test]` (supported by Unity Test Framework's NUnit integration for both Edit Mode and Play Mode).

**How to avoid:**
Use `[UnityTest] IEnumerator MyTest() { yield return SomeUniTaskMethod().ToCoroutine(); }` or `async Task`-based `[Test]` methods (Unity Test Framework supports `async Task` test methods directly) — never block synchronously on a UniTask/Task in a test. Decide upfront which DSM async behaviors need Play Mode tests (anything touching `MonoBehaviour` lifecycle, e.g., `DSMRuntimePanel`) versus Edit Mode tests (pure `DSMSlot`/`DSMSlotManager` logic), since testing the wrong mode gives false confidence that runtime behavior was exercised.

**Warning signs:**
Test methods contain `.GetAwaiter().GetResult()`, `.Result`, or `Task.Wait()`. CI test runs intermittently hang rather than fail. Async runtime behavior (e.g., `DSMWatcher`'s `WatchAsync<T>`) is only tested via synchronous mocks, never actually driven through a real async cycle.

**Phase to address:**
Test Coverage phase — establish the Edit Mode vs Play Mode test convention and the `async Task`/`[UnityTest]` pattern *before* writing the encryption/concurrency test suite, not per-test ad hoc.

---

### Pitfall 14: Tests pass falsely because the broad `catch { return null; }` (or similar swallow) absorbs the exception the test is trying to observe

**What goes wrong:**
A test is written to assert "loading a slot with a wrong encryption key throws/reports an error." But the code path under test (or a caller of it, e.g., `DSMManagerWindow`'s broad catch) still swallows the exception somewhere in the call chain that wasn't updated in this phase, so the test either times out waiting for an exception that never surfaces, or worse, is written loosely enough (`Assert.DoesNotThrow` inverted incorrectly, or checking a nullable return instead of an exception type) that it passes despite the underlying error being silently discarded rather than properly handled.

**Why it happens:**
The exception-handling fix (Pitfall/CONCERNS "Broad Exception Handling") and the test-writing work can land in different phases/PRs; if the swallow isn't fixed first, tests for downstream behavior built on top of it inherit the same blind spot.

**How to avoid:**
Fix the broad `catch` blocks (Bugs & Security phase) *before* or *together with* writing tests that depend on specific exceptions propagating (encryption/validation test suite). Order phases so exception-handling hardening lands before or alongside the test-writing work that depends on it, not after.

**Warning signs:**
A test asserts on a generic "operation failed" signal (e.g., `result == null`) rather than a specific exception type/message, because the specific exception isn't actually observable through the current call chain.

**Phase to address:**
Sequencing concern between Bugs & Security phase and Test Coverage phase — Bugs & Security (broad catch fix) should be an explicit prerequisite/earlier phase, not parallel or later.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Validate key length only, skip entropy/strength checks | Fast to implement and test | False sense that "encryption is now secure"; weak keys still pass | Acceptable if explicitly documented as a known limitation, never as "encryption hardened" |
| Single global `SaveVersion` int instead of per-key migration granularity | Simple first migration | Migration functions balloon in complexity by the 2nd/3rd schema change; can't express partial changes | Acceptable only for the very first version bump if migration steps are still designed as small composable functions internally |
| Reuse `Save()` for key-rotation re-encrypt with no atomic write/backup | Less new code | Unrecoverable data loss if rotation is interrupted | Never acceptable — rotation must have its own transactional guarantee |
| Add `lock`/`ConcurrentDictionary` around `_data` only, skip CTS/debounce race | Smaller diff, easier review | Second known race (`ObjectDisposedException`) ships unfixed | Never acceptable — both races share root cause, fix together |
| Write tests after fixes ("add coverage" as its own later phase) | Feels efficient, avoids touching each file twice | No red/green proof the fix resolves the bug; no safety net during the fix itself | Acceptable only for genuinely new code with no prior known bug; not acceptable for CONCERNS.md-flagged bugs |
| Combine structural refactor (Editor window split) with behavior fixes in the same commits | Fewer PRs | Regressions unattributable; can't bisect | Never acceptable — always separate structural and behavioral changes |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|-------------------|
| UniTask (async save/load) | Blocking on `.GetAwaiter().GetResult()`/`.Result` inside Edit Mode tests, causing hangs instead of failures | Use `[UnityTest] IEnumerator` + `.ToCoroutine()`, or `async Task`-returning `[Test]` methods; never block synchronously on UniTask in tests |
| UniTask (git dependency tracking `main`) | Pinning thread-safety/test-suite work atop a moving-target dependency; a UniTask update mid-milestone changes scheduling/behavior underneath the fix | Pin the UniTask git dependency to a specific commit (already flagged in CONCERNS.md) *before* starting thread-safety work, so behavior is stable while you're reasoning about races |
| Unity Test Framework (asmdef setup) | Test assembly references `UnityEditor` and breaks non-Editor players, or Edit Mode/Play Mode tests are placed in the wrong assembly | Create separate `*.Tests.Runtime.asmdef` (Play Mode, no `UnityEditor` ref) and `*.Tests.Editor.asmdef` (Edit Mode, `UnityEditor` ref, `includePlatforms: ["Editor"]`), mirroring the existing `Runtime`/`Editor` split already in the package |
| Newtonsoft.Json (encryption/serialization) | Assuming JSON parse errors always throw a specific type; broad catches mask `JsonReaderException` vs `JsonSerializationException` distinctions needed for good error messages | When fixing broad catches, catch and message-differentiate `JsonReaderException` (malformed JSON) separately from type-mismatch `JsonSerializationException` |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Adding a `lock` around all of `Save()`/`Load()` including file I/O and encryption (not just the dictionary) | Main-thread stalls, frame drops during autosave in gameplay | Lock only the in-memory dictionary access; keep file I/O/encryption outside the lock or fully async without holding a lock across it | Noticeable once autosave/debounce fires during active gameplay with larger save payloads |
| Per-migration-step full-slot reconstruction (Pitfall 7) called on every load, even when no migration is needed | Load time increases as migration chain grows across versions | Check version once, short-circuit to zero-cost path when `savedVersion == currentVersion`; only run the migration chain when versions differ | Becomes noticeable after 3+ schema versions accumulate for long-lived players |
| Migration/versioning logs firing at INFO/WARN on every single load, even for up-to-date saves | Console/log noise makes real migration events (Pitfall 9) hard to spot | Log only when an actual migration step executes, not on every load | Immediately, once versioning ships without a "no-op" fast path |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Treating "reject empty/short key" as "encryption is now secure" | Weak/low-entropy keys still produce weak derived keys; false confidence in the security posture | Document explicitly that DSM validates presence/length only, not strength; strength is the consuming project's responsibility |
| Rotating keys via direct overwrite with no backup/atomicity | A crash or interrupted process during rotation makes the save permanently unrecoverable with either key | Temp-file write + verify + atomic replace; keep old key in memory until new file is confirmed durable |
| Discarding the old encryption key from memory/config before rotation is verified complete | Same as above — no way to retry or recover if the new write is bad | Only overwrite `DSMConfig`'s key field after the rotated file passes a round-trip verification read |
| Logging key material or full decrypted payloads in error messages when fixing the broad `catch` blocks | New logging added to "fix" silent failures accidentally leaks sensitive save data or key fragments to the Editor console/log files | When adding error visibility per the exception-handling fix, log exception type/message and slot/key *names* only — never log key values or decrypted content |

## "Looks Done But Isn't" Checklist

- [ ] **Key validation:** Often only fixed in `DSMSlot`, not in `DSMManagerWindow`'s independent key-read path — verify every call site that reads `EncryptionKey` goes through one shared validated accessor.
- [ ] **Thread-safety:** Often only fixes `_data` dictionary access — verify `ScheduleSave()`/`DebouncedSaveAsync()` CTS lifecycle is fixed in the same pass, and verify tests assert final-state correctness, not just "no exception."
- [ ] **Migration:** Often only handles "new key gets default," silently discarding renamed-key data — verify each migration step explicitly enumerates renamed/removed keys rather than relying on default fallback.
- [ ] **Editor window split:** Often creates new classes that still share raw mutable fields (selected slot, foldout state) instead of clean interfaces — verify no `public` mutable field is shared as the seam between the new classes, and verify IMGUI control order is unchanged between Layout/Repaint after the split.
- [ ] **Test coverage:** Often adds tests only for the happy path of each fix — verify each CONCERNS.md-flagged bug has a corresponding test that was confirmed to fail against pre-fix code (red/green), and that async tests use `[UnityTest]`/`async Task`, never blocking calls.
- [ ] **Key rotation:** Often reuses plain `Save()` with no atomicity — verify rotation writes to a temp path, verifies round-trip, and only then replaces the original file.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|------------------|
| Key rotation corrupts a save mid-rotation (Pitfall 3) | HIGH (if no backup) / LOW (if backup kept) | If a `.bak` or temp-file-first pattern was used, restore from backup and re-run rotation with fixed atomicity. If not, data is likely unrecoverable — this is exactly why atomic rotation must be built correctly the first time. |
| Migration silently drops renamed-key data (Pitfall 9) | MEDIUM | Requires manually inspecting old save files (if retained) to recover the discarded values and writing a targeted one-off remap; add the missing explicit remap to the migration function going forward. |
| IMGUI control-ID mismatch surfaces after Editor window split (Pitfall 10) | LOW | Usually fixable by re-auditing the specific conditional block that changed control count between Layout/Repaint; add a regression note but no data is at risk (Editor-only UI bug). |
| Concurrency test suite passes but real race still exists (Pitfall 6) | MEDIUM | Rewrite the test to assert final-state correctness and run repeatedly/with deterministic interleaving; re-audit the "fixed" code for compound check-then-act operations on `ConcurrentDictionary`. |
| Blocking async test hangs CI (Pitfall 13) | LOW | Convert the offending test to `[UnityTest]`/`async Task` pattern; add a CI timeout as a safety net so future instances fail fast instead of hanging the pipeline. |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| 1. Inconsistent key validation across call sites | Bugs & Security (encryption validation) | `grep` audit finds a single shared key-accessor; no raw `EncryptionKey` reads outside it |
| 2. Length-only validation mistaken for strength | Bugs & Security (encryption validation) | README/code comment explicitly states the strength limitation; test suite doesn't imply "secure" for any passing key |
| 3. Non-atomic key rotation | Missing Critical Features (key rotation) | Test simulates interruption mid-rotation; original file remains intact and readable with old key |
| 4. Partial thread-safety fix (dictionary only) | Fragile Areas (thread-safety) | Diff includes both `_data` synchronization and `ScheduleSave()`/CTS lifecycle fix |
| 5. Async re-entrancy misdiagnosed as OS threading | Fragile Areas (thread-safety) | Design doc/plan explicitly names the hazard type (re-entrancy vs true concurrency) before implementation |
| 6. "No exception" concurrency tests hiding lost writes | Test Coverage (concurrency tests) | Concurrency tests assert final data state, not just absence of exceptions |
| 7. Global version field can't express partial migrations | Missing Critical Features (versioning) | Migration interface is composable per-step, not one monolithic full-slot rebuild |
| 8. Migration has no regression fixtures | Test Coverage + Missing Critical Features (versioning) | `TestFixtures/` directory with versioned sample saves exists and is exercised by tests |
| 9. Migration silently defaults instead of remapping | Missing Critical Features (versioning) | Migration step enumerates renamed/removed keys explicitly; logs fire on actual transform |
| 10. IMGUI control-order break during Editor split | Tech Debt & Performance (Editor window split) | Manual smoke test across slot switch/delete/foldout state combos; no `ArgumentException` in console |
| 11. Refactor + bug-fix bundled in same commits | Bugs & Security + Tech Debt & Performance (sequenced) | Git history shows structural-only commits separate from behavior-fix commits |
| 12. Tests written after fixes, not driving them | Every phase touching a CONCERNS.md bug | Each phase's plan includes "write failing test first" as an explicit step |
| 13. Blocking UniTask calls in Edit Mode tests | Test Coverage (test infra setup) | No `.GetAwaiter().GetResult()`/`.Result`/`.Wait()` in test code; CI has a timeout as a backstop |
| 14. Broad catch swallows exceptions tests depend on | Bugs & Security (before/with Test Coverage) | Exception-handling fix lands before or with the tests that assert specific exception types |

## Sources

- [UnityCsReference — GUILayoutUtility.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUILayoutUtility.cs/) — IMGUI control-ID/layout mechanics (HIGH confidence, official source)
- [Unity Manual — IMGUI Controls](https://docs.unity3d.com/2022.3/Documentation/Manual/gui-Controls.html) — official documentation on control ID allocation across Layout/Repaint events (HIGH confidence)
- [Going deep with IMGUI and Editor Customization — Unity Blog](https://blog.unity.com/technology/going-deep-with-imgui-and-editor-customization) — explains why control call order must match across event types (HIGH confidence, official)
- [Cysharp/UniTask — GitHub](https://github.com/Cysharp/UniTask) — async/await semantics for Unity, awaiter reuse constraints (HIGH confidence, official repo)
- [Async await in Unittests — Unity Discussions](https://discussions.unity.com/t/async-await-in-unittests/689336) — community-reported deadlock patterns with blocking async calls in Edit Mode tests (MEDIUM confidence, community)
- [Support for async/await in tests — Unity Discussions](https://discussions.unity.com/t/support-for-async-await-in-tests/767865) — confirms `async Task`/`[UnityTest]` patterns for Unity Test Framework (MEDIUM confidence, community)
- Encryption key-rotation atomicity/exposure-window concepts synthesized from general distributed-systems key-management engineering practice (MEDIUM confidence — general software engineering domain knowledge, not Unity-specific)
- `.planning/codebase/CONCERNS.md` (2026-07-08 audit) — source of the specific fragile areas this document goes one level deeper on (HIGH confidence, first-party project audit)

---
*Pitfalls research for: Unity game save-system (DataSaveManager) hardening milestone*
*Researched: 2026-07-08*
