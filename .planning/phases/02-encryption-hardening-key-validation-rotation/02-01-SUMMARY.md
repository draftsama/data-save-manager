---
phase: 02-encryption-hardening-key-validation-rotation
plan: 01
subsystem: encryption
tags: [aes-cbc, hmac-sha256, encrypt-then-mac, pbkdf2, unity, nunit]

# Dependency graph
requires:
  - phase: 01-foundation-thread-safety-robustness-test-infrastructure
    provides: NUnit EditMode test infrastructure, DSMTestConfig reflection-based fixture builder, DSMSlotNameValidator static-validator pattern to mirror
provides:
  - "DSMEncryptionKey.Validate — single shared accessor rejecting empty/null/whitespace/too-short encryption keys"
  - "Encrypt-then-MAC DSMEncryptor (AES-256-CBC + HMAC-SHA256), tamper/truncation/wrong-key detection via DSMEncryptionException"
  - "DSMConfig.SetEncryptionKey validation wiring"
  - "DSMTestConfig.Create(encryptionKey:) for encrypted-slot test fixtures"
affects: [02-02-key-rotation, phase-05-editor-tooling]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Encrypt-then-MAC: HMAC-SHA256 computed over magic+version+salt+IV+ciphertext, verified with CryptographicOperations.FixedTimeEquals BEFORE any AES decrypt is attempted"
    - "Single PBKDF2 derivation (64 bytes) split into distinct AES key (first 32) and MAC key (last 32) — never reuse key material across primitives"
    - "Static validator + ArgumentException chokepoint pattern (mirrors DSMSlotNameValidator from Phase 1), called from every runtime and Editor entry point"

key-files:
  created:
    - Runtime/DSMEncryptionKey.cs
    - Tests/Editor/DSMEncryptionKeyTests.cs
    - Tests/Editor/DSMEncryptorTests.cs
  modified:
    - Runtime/DSMEncryptor.cs
    - Runtime/DSMConfig.cs
    - Tests/Editor/DSMTestConfig.cs

key-decisions:
  - "MinLength = 8 for DSMEncryptionKey (Claude's Discretion, documented in code) — a defensible floor, not a strength guarantee"
  - "Iterations = 600000 for PBKDF2-HMAC-SHA256 (OWASP current guidance, Claude's Discretion, documented in code) — flagged for human benchmarking on target hardware per STATE.md concern"
  - "Format version bumped to DSM2/2 — no backward compatibility with old .enc files, per PROJECT.md's explicit breaking-change allowance"

patterns-established:
  - "Encrypt-then-MAC framed buffer layout: magic(4) + version(1) + salt(32) + IV(16) + ciphertext + tag(32)"

requirements-completed: [BUGS-02, ENC-01]
# TEST-02 is only PARTIALLY complete after this plan: wrong-key, truncated-file, and
# empty-key scenarios are covered and passing, but key-change-between-saves is
# explicitly deferred to Plan 02-02 per this plan's own success_criteria. Not marked
# complete in REQUIREMENTS.md traceability until 02-02 lands the remaining scenario.

coverage:
  - id: D1
    description: "DSMEncryptionKey.Validate rejects null/empty/whitespace/too-short keys with ArgumentException, accepts keys >= MinLength, and never echoes the key in its message"
    requirement: "BUGS-02"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMEncryptionKeyTests.cs#Validate_NullKey_ThrowsArgumentException,Validate_EmptyKey_ThrowsArgumentException,Validate_WhitespaceKey_ThrowsArgumentException,Validate_TooShortKey_ThrowsArgumentException,Validate_ValidKey_DoesNotThrow,Validate_ExceptionMessage_DoesNotLeakKeyValue"
        status: pass
    human_judgment: true
    rationale: "Verified by executing the same source files in a standalone net10 console harness (11/11 pass) plus dotnet build 0-errors across all three Unity assemblies; Unity Editor was open this session (batchmode -runTests blocked, per the Phase 1 finding), so a human must still confirm green in Unity Test Runner before this is fully proven in-engine."
  - id: D2
    description: "DSMEncryptor rewritten to Encrypt-then-MAC — round-trips valid data and throws DSMEncryptionException on tamper, truncation, or wrong key instead of returning corrupted plaintext"
    requirement: "ENC-01"
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMEncryptorTests.cs#EncryptThenDecrypt_ValidKey_RoundTripsOriginalJson,Decrypt_TamperedCiphertext_ThrowsDSMEncryptionException,Decrypt_TruncatedBuffer_ThrowsDSMEncryptionException,Decrypt_WrongKey_ThrowsDSMEncryptionException,Encrypt_EmptyKey_ThrowsArgumentException"
        status: pass
    human_judgment: true
    rationale: "Same verification method as D1 (standalone net10 harness + dotnet build), same Unity Test Runner confirmation still open."
  - id: D3
    description: "MAC tag comparison is constant-time (CryptographicOperations.FixedTimeEquals) and no exception/log message in DSMEncryptor.cs, DSMEncryptionKey.cs, or DSMConfig.cs interpolates key or derived key material"
    requirement: "ENC-01"
    verification:
      - kind: other
        ref: "grep -nE 'password|key|EncryptionKey' Runtime/DSMEncryptor.cs | grep -iE 'Debug\\.Log|Exception\\(' (0 matches); grep -n FixedTimeEquals Runtime/DSMEncryptor.cs (1 match)"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-07-13
status: complete
---

# Phase 02 Plan 01: Encryption Hardening — Key Validation and Encrypt-then-MAC Summary

**Rewrote DSMEncryptor from AES-CBC-only to Encrypt-then-MAC (AES-256-CBC + HMAC-SHA256) and introduced DSMEncryptionKey.Validate as the single shared key-validation chokepoint used by both DSMConfig and the DSMEncryptor runtime/Editor entry points.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-13T12:01:21Z
- **Completed:** 2026-07-13T12:08:41Z
- **Tasks:** 2 (TDD: RED then GREEN)
- **Files modified:** 6 (3 created, 3 modified)

## Accomplishments
- `DSMEncryptionKey.Validate(string?)` rejects null/empty/whitespace/too-short (< 8 char) keys via `ArgumentException`, called from `DSMEncryptor.Encrypt`/`Decrypt` and `DSMConfig.SetEncryptionKey` — the single chokepoint every runtime and Editor path (DSMSlot, DSMConfigEditor, DSMManagerWindow) flows through.
- `DSMEncryptor` now produces Encrypt-then-MAC buffers: `[magic "DSM2"(4)][version(1)][salt(32)][IV(16)][ciphertext][HMAC-SHA256 tag(32)]`. The MAC is verified with `CryptographicOperations.FixedTimeEquals` before any AES-CBC decrypt is attempted.
- A tampered byte, a truncated buffer, or the wrong key all throw the same specific `DSMEncryptionException` — never a generic crypto exception, never corrupted plaintext.
- Full edge-case test suite (`DSMEncryptionKeyTests`, `DSMEncryptorTests`) lands RED-then-GREEN per TDD, covering round-trip, tamper, truncation, wrong-key, and empty-key rejection.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing edge-case tests for key validation and Encrypt-then-MAC (RED)** - `bbc7b2f` (test)
2. **Task 2: Implement DSMEncryptionKey.Validate + rewrite DSMEncryptor to Encrypt-then-MAC (GREEN)** - `0864657` (feat)

**Plan metadata:** (this commit, follows)

## Files Created/Modified
- `Runtime/DSMEncryptionKey.cs` - New static validator: `MinLength = 8`, `Validate(string?)` throwing `ArgumentException` on null/empty/whitespace/too-short
- `Runtime/DSMEncryptor.cs` - Rewritten to Encrypt-then-MAC (AES-256-CBC + HMAC-SHA256); adds `DSMEncryptionException` type; public `Encrypt(string,string)`/`Decrypt(byte[],string)` signatures preserved
- `Runtime/DSMConfig.cs` - `SetEncryptionKey` now calls `DSMEncryptionKey.Validate` before assigning
- `Tests/Editor/DSMTestConfig.cs` - Adds optional `encryptionKey` parameter to `Create(...)`
- `Tests/Editor/DSMEncryptionKeyTests.cs` - New: 6 tests covering null/empty/whitespace/too-short/valid/no-leak
- `Tests/Editor/DSMEncryptorTests.cs` - New: 5 tests covering round-trip, tamper, truncation, wrong-key, empty-key

## Decisions Made
- `DSMEncryptionKey.MinLength = 8` — documented in-code as Claude's Discretion; a defensible floor for a developer-set key, not a cryptographic strength claim. A human may raise this for their own threat model.
- PBKDF2 `Iterations = 600000` — OWASP current guidance for PBKDF2-HMAC-SHA256, documented in-code as Claude's Discretion since STATE.md flagged this as needing local hardware benchmarking and research was disabled this run. Comment states: do not drop below ~100000; a human may revisit after benchmarking.
- Format bumped to magic `"DSM2"` / version `2` with zero backward compatibility for old `.enc` files — consistent with PROJECT.md's explicit "breaking changes are acceptable" constraint.
- Single 64-byte PBKDF2 output split into two independent 32-byte keys (AES, MAC) rather than deriving them separately or reusing one key for both primitives — standard Encrypt-then-MAC key-separation practice.

## Deviations from Plan

None — plan executed exactly as written. Both tasks completed with the exact file set and behavior specified in `02-01-PLAN.md`.

## Issues Encountered

- **Stale generated `.csproj` files blocked direct verification.** The Unity-generated `DMS.Tests.Editor.csproj` (gitignored, `EnableDefaultItems=false`) did not include the newly created test files or `DSMEncryptionKey.cs` because Unity's Editor domain hadn't regenerated project files since these files were added on disk. Resolved by temporarily adding explicit `<Compile Include>` entries to the gitignored `.csproj` files (restored to their pre-edit content afterward — these files are `.gitignore`d and Unity will regenerate them on its own next domain reload regardless).
- **Unity Editor open, blocking `-runTests` batchmode** — same finding as Phase 01 (see STATE.md blocker note). Verified GREEN two ways instead: (1) `dotnet build -t:Rebuild` with 0 errors across `DMS.Runtime.csproj`, `DSM.Editor.csproj`, and `DMS.Tests.Editor.csproj`; (2) copied `DSMEncryptionKey.cs` and `DSMEncryptor.cs` (both pure BCL, no UnityEngine dependency) into a standalone net10 console harness and executed all 11 test assertions directly — 11/11 passed. This is strong evidence of correctness but is not a substitute for Unity Test Runner; flagged below for human confirmation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02-02 (encryption key rotation) can build directly on `DSMEncryptionKey.Validate` and the new Encrypt-then-MAC format — no further groundwork needed here.
- **Open item for a human:** open Unity Test Runner (EditMode) once the Editor is free to compile/reload, and confirm `DSMEncryptionKeyTests` (6 tests) and `DSMEncryptorTests` (5 tests) are green, alongside the existing 34 Phase-1 tests. This mirrors the same open item Phase 01 left (STATE.md blocker note) and should be closed out at the same time.
- `DSMEncryptor`'s public signatures (`Encrypt(string,string)`, `Decrypt(byte[],string)`) are unchanged, so `DSMSlot`, `DSMConfigEditor`, and `DSMManagerWindow` remain source-compatible without modification — confirmed via `grep` (no other call-site changes needed) and via clean `DSM.Editor.csproj` build.

---
*Phase: 02-encryption-hardening-key-validation-rotation*
*Completed: 2026-07-13*

## Self-Check: PASSED

- FOUND: Runtime/DSMEncryptionKey.cs
- FOUND: Tests/Editor/DSMEncryptionKeyTests.cs
- FOUND: Tests/Editor/DSMEncryptorTests.cs
- FOUND: commit bbc7b2f
- FOUND: commit 0864657
