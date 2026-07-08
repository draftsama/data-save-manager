# Phase 1: Foundation — Thread-Safety, Robustness & Test Infrastructure - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning

<domain>
## Phase Boundary

DSM's shared state (`DSMSlot._data`, `DSMSlotManager._slots`) and save/load pipeline become safe under concurrent access and malformed/invalid input, verified by a new automated test suite. Covers: thread-safety for slot data and the debounce/CTS race, atomic write-temp-then-rename for saves, slot-name and JSON-load input validation, widget-missing-component warning, and standing up `Tests/Editor` + `Tests/Runtime` asmdefs with concurrency and invalid-input regression tests.

Requirements: CONC-01, CONC-02, CONC-03, CONC-04, BUGS-03, BUGS-04, TEST-01, TEST-03, TEST-04.

</domain>

<decisions>
## Implementation Decisions

### Debounce / CTS Race (CONC-02)
- **D-01:** Replace the per-`Set()` CancellationTokenSource create/dispose pattern in `DSMSlot.ScheduleSave()` with a single reusable delay loop per slot — track "last requested save time" and have one long-lived debounce task check elapsed time against it, rather than cancel-and-recreate a CTS on every call. Eliminates the disposal race at the root instead of guarding around it.

### Invalid Input Handling (BUGS-03, part of CONC-04/TEST-04)
- **D-02:** Malformed/corrupt JSON encountered during `Load()` does NOT throw — log a warning (with slot name and underlying parse error) and fall back to seeding defaults from `DSMConstant`, same as the "no save file" path. Prioritizes players staying unblocked over strict-failure visibility.
- **D-03:** Slot name validation is strict: reject anything outside alphanumeric + `_`/`-`, plus path separators (`/`, `\`), `..`, and Windows-reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`, case-insensitive). Matches the CONCERNS.md-recommended rule set exactly — no more permissive fallback.

### Locking Strategy (CONC-01)
- **D-04:** Per-slot locking granularity — each `DSMSlot` owns its own synchronization primitives; `DSMSlotManager._slots` gets its own separate (coarse) lock scoped only to add/remove-slot operations. Rejected a single global lock: it would serialize all slots' operations against each other, defeating multi-slot concurrency for no benefit.
- **D-05:** Split primitives within a slot: plain `lock` around `_data` dictionary mutations (sync-only, short critical section, used by `Set`/`Get`/`Has`/`Delete`) — separate from a per-slot `SemaphoreSlim` used as the I/O gate around `Load`/`Save`/`Rotate`, since those span `await` boundaries and a plain `lock` cannot be held across `await`. This split was chosen over a single `SemaphoreSlim` for everything to keep the hot in-memory path (`Set`/`Get`) fast and fully synchronous.

### Test Scope (TEST-01, TEST-03, TEST-04)
- **D-06:** EditMode tests only for this phase — no PlayMode/`[UnityTest]` tests. `DSMSlot` doesn't depend on MonoBehaviour lifecycle; async/UniTask behavior (debounce delay loop, concurrent Set+Save, concurrent Set+Load) can be tested as `async Task`/`UniTask` EditMode tests. Faster and simpler CI. Note: `Tests/Runtime` asmdef (per TEST-01/roadmap) still exists as an asmdef referencing the Runtime assembly — it is not the same thing as PlayMode execution; both `Tests/Editor` and `Tests/Runtime` asmdefs run in Edit Mode via `includePlatforms: ["Editor"]`.

### Claude's Discretion
- Exact wording/type of the log-warning message on JSON parse failure, and exact exception type thrown by `DSMEncryptor`/JSON parser internally before being caught — left to planner/executor judgment.
- Specific `SemaphoreSlim` acquire/release helper shape (e.g., `IDisposable`-based scope guard) — implementation detail, not a decision the user needs to weigh in on.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Concerns audit (source of Active requirements)
- `.planning/codebase/CONCERNS.md` §"Fragile Areas" (Thread Safety of DSMSlot._data, Debounce Timer Disposal in ScheduleSave()) — root-cause description of CONC-01/CONC-02
- `.planning/codebase/CONCERNS.md` §"Missing Slot Name Validation" — exact validation rule set adopted verbatim in D-03
- `.planning/codebase/CONCERNS.md` §"Silent Widget Component Errors" — BUGS-04 root cause

### Architecture
- `.planning/codebase/ARCHITECTURE.md` — layered component map; `DSMSlot`/`DSMSlotManager` responsibilities and existing data flow (Set→Watch, Load→SeedDefaults)

### Testing conventions
- `.planning/codebase/TESTING.md` — recommended asmdef structure (`Tests/Editor`, `Tests/Runtime`), NUnit patterns, async/UniTask test examples already sketched for this codebase

### Research synthesis
- `.planning/research/SUMMARY.md` §"Phase 1: Thread-Safety Foundation + Test Infrastructure" — confirms Phase 1 uses standard, well-documented patterns (SemaphoreSlim for async-spanning critical sections); flagged as **not** needing a deeper `--research-phase` pass
- `.planning/research/PITFALLS.md` — pitfalls 1 ("fixing `_data` but leaving CTS race unsynchronized"), 5 ("no-exception-thrown concurrency tests"), 12 ("tests written after fixes") apply directly to this phase's Definition of Done

### Project-level
- `.planning/PROJECT.md` — Core Value statement (no silent data loss/corruption); no-backward-compatibility decision (permits strict slot-name rejection and behavior changes without back-compat shims)
- `.planning/REQUIREMENTS.md` — full requirement text for CONC-01..04, BUGS-03/04, TEST-01/03/04

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DSMConstant` reflection-based default seeding (`DSMSlot.SeedDefaults()`) — reuse as-is for the malformed-JSON fallback path (D-02); no new default-seeding logic needed.
- Existing `DSM.Configure()` test-injection seam (`Runtime/DSM.cs:13-19`) — already documented in ARCHITECTURE.md as the intended way to inject test doubles; use it in the new test suite instead of adding a new DI mechanism.

### Established Patterns
- `DSMWatcher` already uses `lock` for thread-safe channel registration — precedent for the plain-`lock` choice on `_data` (D-05) rather than introducing a new concurrency primitive style into the codebase.
- Silent-fallback-with-warning is the codebase's existing error-handling convention (`SeedDefaults()` catches per-field exceptions and logs warnings) — D-02 follows this established convention rather than introducing throw-based error handling as a new pattern for this one path.

### Integration Points
- `DSMSlot.Set/Get/Has/Delete` — gains the `lock` from D-05.
- `DSMSlot.Load/Save/SaveAsync` — gains the `SemaphoreSlim` I/O gate from D-05, and the temp-write-then-rename sequence for CONC-03.
- `DSMSlot.ScheduleSave()` — full rewrite of debounce mechanism per D-01.
- `DSMSlotManager.GetOrCreateSlot/DeleteSlot` — gains the coarse slot-dictionary lock from D-04.
- `DSMSlotManager.UseSlot()` → `DSMSlot.Load()` — malformed-JSON fallback (D-02) sits here.
- New: `Tests/Editor/*.asmdef`, `Tests/Runtime/*.asmdef` per TEST-01.

</code_context>

<specifics>
## Specific Ideas

No specific UI/UX references — this is a pure runtime/infrastructure phase. The Windows-reserved-name list for slot validation is explicit and non-negotiable: `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9` (case-insensitive), per CONCERNS.md.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No scope-creep suggestions arose during this discussion.

</deferred>

---

*Phase: 1-Foundation — Thread-Safety, Robustness & Test Infrastructure*
*Context gathered: 2026-07-08*
