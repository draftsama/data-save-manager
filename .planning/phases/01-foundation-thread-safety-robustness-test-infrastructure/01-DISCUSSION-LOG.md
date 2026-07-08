# Phase 1: Foundation — Thread-Safety, Robustness & Test Infrastructure - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-08
**Phase:** 1-Foundation — Thread-Safety, Robustness & Test Infrastructure
**Areas discussed:** Debounce/CTS race fix, Invalid input behavior, Lock granularity, Test scope (Edit vs Play)

---

## Debounce/CTS Race Fix

| Option | Description | Selected |
|--------|-------------|----------|
| Cancel-and-restart CTS + guard | Keep existing pattern (new CTS every call) but add lock/guard around disposal | |
| Single reusable delay loop | One long-lived debounce task per slot lifetime; tracks "last requested time," no CTS churn | ✓ |
| You decide | Claude picks from existing codebase patterns | |

**User's choice:** Single reusable delay loop
**Notes:** Chosen over the cancel-and-restart guard approach because it removes the root cause (CTS churn) instead of adding defensive guards around it.

---

## Invalid Input Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Log warn + fallback defaults | Malformed JSON on Load logs a warning and seeds DSMConstant defaults, same as missing-file path | ✓ |
| Throw specific exception | Throw a dedicated exception (e.g. DSMCorruptSaveException) for caller to handle | |

**User's choice:** Log warn + fallback defaults

### Slot Name Validation Follow-up

| Option | Description | Selected |
|--------|-------------|----------|
| Strict: alphanumeric+_- only | Reject path separators, `..`, Windows reserved names, and all other special characters | ✓ |
| Lenient: block traversal only | Reject only path separators/dots/reserved names; allow other special characters | |

**User's choice:** Strict: alphanumeric+_- only
**Notes:** Matches the exact rule set already recommended in CONCERNS.md.

---

## Lock Granularity

| Option | Description | Selected |
|--------|-------------|----------|
| Per-slot lock (recommended) | Each DSMSlot owns its own lock/semaphore; DSMSlotManager._slots gets a separate coarse lock for add/remove only | ✓ |
| Single global lock | One lock covers manager + all slots | |

**User's choice:** Per-slot lock (recommended)

### Primitive Split Follow-up

| Option | Description | Selected |
|--------|-------------|----------|
| Split: lock + SemaphoreSlim | Plain `lock` for sync `_data` access; separate `SemaphoreSlim` I/O gate for Load/Save/Rotate (spans await) | ✓ |
| Single SemaphoreSlim for everything | One SemaphoreSlim covers both _data access and I/O | |

**User's choice:** Split: lock + SemaphoreSlim
**Notes:** Keeps the hot in-memory Set/Get path fully synchronous and fast; SemaphoreSlim reserved for operations that actually span `await`.

---

## Test Scope (Edit vs Play)

| Option | Description | Selected |
|--------|-------------|----------|
| EditMode only | Faster, simpler CI; DSMSlot doesn't depend on MonoBehaviour lifecycle | ✓ |
| EditMode + PlayMode split | Add PlayMode tests for real frame-based timing (e.g. debounce delay) | |

**User's choice:** EditMode only

---

## Claude's Discretion

- Exact log-warning message wording and specific caught exception types for JSON parse failures
- Specific SemaphoreSlim acquire/release helper shape (e.g., IDisposable scope guard)

## Deferred Ideas

None — discussion stayed entirely within Phase 1 scope.
