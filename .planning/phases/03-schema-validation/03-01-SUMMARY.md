---
phase: 03-schema-validation
plan: 01
subsystem: data-persistence
tags: [unity, newtonsoft-json, reflection, nunit, schema-validation]

requires:
  - phase: 01-foundation-core
    provides: DSMSlot Set/Get, DSMSlotManager slot lifecycle, DSMConstant codegen
  - phase: 02-encryption-hardening-key-validation-rotation
    provides: DSMConfig serialized-field conventions, DSMTestConfig builder pattern
provides:
  - DSMSchema — a per-Type-cached key->CLR-type map reflected from DSMConstant's public static fields
  - DSMSchemaViolationException (value-free message: key + expected/actual type names only)
  - DSMConfig.StrictSchema flag (serialized `_strictSchema`, default false = lenient)
  - DSMSlot.Set<T>/Get<T> validation gate: strict throws, lenient warns-and-coerces (Set) / warns (Get)
affects: [04-widget-schema-integration, any future phase touching DSMSlot.Set/Get or DSMConfig]

tech-stack:
  added: []
  patterns:
    - "Per-Type static cache guarded by a lock, mirroring DSMSlotManager.ResolveConstantType's cache-by-Type pattern"
    - "Colocated exception class in the same file as its owning feature (DSMSchemaViolationException beside DSMSchema, mirroring DSMRotationInterruptedException beside DSMSlotManager)"
    - "Value-free exception/warning messages (key + type names only) — same leak-safety convention as DSMEncryptionKey.Validate"

key-files:
  created:
    - Runtime/DSMSchema.cs
    - Tests/Editor/DSMSchemaValidationTests.cs
  modified:
    - Runtime/DSMConfig.cs
    - Runtime/DSMSlot.cs
    - Tests/Editor/DSMTestConfig.cs

key-decisions:
  - "DSMSchema.For(null) returns a shared static empty-schema singleton rather than allocating per call, since the null/no-DSMConstant path is the common case for consumers without codegen."
  - "Lenient-mode Set coercion round-trips through JToken.FromObject(value).ToObject(expected) and re-wraps the result via JToken.FromObject before storing, so the persisted token always matches the schema-declared type when coercion succeeds."
  - "Lenient-mode Get does not coerce/rewrite the stored token — it warns and lets the existing token.ToObject<T>() ?? defaultValue path do its normal best-effort read, so no behavior beyond the warning was added to the Get path."

requirements-completed: [SCHM-01, SCHM-02]

coverage:
  - id: D1
    description: "DSMSchema reflects DSMConstant's public static fields into a key->Type map, cached per Type; DSMSchema.For(null) yields an empty pass-through schema"
    requirement: SCHM-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#EmptySchema_NullConstantType_IsPassThroughNoOp"
        status: unknown
    human_judgment: true
    rationale: "Project constraint forbids running Unity EditMode tests from this agent (batchmode has deadlocked this project before); the test file compiles against the new symbols and the automated dotnet build passed, but the human must run Unity Test Runner to confirm the assertions actually pass."
  - id: D2
    description: "DSMSlot.Set/Get throw DSMSchemaViolationException on typeof(T) mismatch in strict mode"
    requirement: SCHM-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Strict_SetTypeMismatch_ThrowsSchemaViolation"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Strict_GetTypeMismatch_ThrowsSchemaViolation"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint as D1 — cannot execute EditMode tests from this agent."
  - id: D3
    description: "DSMConfig.StrictSchema defaults to lenient (warn+coerce); mismatches never throw and existing call sites/serialized assets are unaffected"
    requirement: SCHM-02
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Lenient_Mismatch_CoercesToSchemaType"
        status: unknown
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Lenient_UncoercibleMismatch_DoesNotThrow"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint as D1 — cannot execute EditMode tests from this agent."
  - id: D4
    description: "No warning/exception message leaks the stored or attempted value"
    requirement: SCHM-01
    verification:
      - kind: unit
        ref: "Tests/Editor/DSMSchemaValidationTests.cs#Strict_ExceptionMessage_DoesNotLeakOffendingValue"
        status: unknown
    human_judgment: true
    rationale: "Same Unity Test Runner constraint as D1 — cannot execute EditMode tests from this agent."

duration: ~10min
completed: 2026-07-15
status: complete
---

# Phase 3 Plan 1: Schema Validation Summary

**Set/Get now validate typeof(T) against a DSMConstant-derived schema — strict mode throws DSMSchemaViolationException, lenient mode (default) warns and coerces via Newtonsoft, and no message ever leaks a value.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-07-15T10:52:00+07:00 (approx, per session context)
- **Completed:** 2026-07-15T10:59:05+07:00
- **Tasks:** 2 completed (RED test spec, GREEN implementation)
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments
- New `DSMSchema` reflects DSMConstant's public static fields (`field.Name -> field.FieldType`) into a per-Type-cached map — the SAME fields `DSMSlot.SeedDefaults` already reads, so key types are never maintained twice. `DSMSchema.For(null)` returns a shared empty schema (pass-through no-op for consumers without a DSMConstant).
- `DSMConfig.StrictSchema` (backed by serialized `_strictSchema`, default `false`) gates behavior without any call-site changes — every existing `Set`/`Get` call and serialized `DSMConfig` asset keeps working unchanged after upgrade.
- `DSMSlot.Set<T>`/`Get<T>` gate on the schema: strict mode throws `DSMSchemaViolationException` (message contains only the key name and expected/actual type names — never the value); lenient mode warns and, for `Set`, coerces the value into the schema type via a Newtonsoft round-trip (falling back to storing the original value unchanged if coercion itself throws).
- A new EditMode NUnit fixture (`DSMSchemaValidationTests`) fully specifies the RED->GREEN contract: strict-throw (Set/Get), strict-match no-throw, unconstrained-key passthrough, lenient-coerce, lenient-uncoercible-no-throw, no-value-leak, and the empty-schema/null-constant no-op.

## Task Commits

Each task was committed atomically:

1. **Task 1: Author failing EditMode spec for schema validation (RED)** - `63dc9a4` (test)
2. **Task 2: Implement DSMSchema, StrictSchema flag, and Set/Get validation gate (GREEN)** - `7bd66c7` (feat)

**Plan metadata:** commit pending (docs: complete plan)

## TDD Gate Compliance

RED gate confirmed: `63dc9a4 test(03-01): add failing schema validation spec (RED)`.
GREEN gate confirmed: `7bd66c7 feat(03-01): implement DSMSchema and Set/Get validation gate (GREEN)`, landed after RED.
No REFACTOR commit was needed — the implementation matched the plan's design on the first pass; `dotnet build DMS.Runtime.csproj` reported 0 errors after Task 2 with no follow-up cleanup required.

## Files Created/Modified
- `Runtime/DSMSchema.cs` - New: `DSMSchema` (static per-Type cache, `For(Type?)`, `TryGetExpectedType`, `Count`) and colocated `DSMSchemaViolationException`
- `Runtime/DSMConfig.cs` - Added serialized `_strictSchema` field and public `StrictSchema` accessor, alongside the existing `_encrypt`/`Encrypt` pair
- `Runtime/DSMSlot.cs` - Added `_schema` field built via `DSMSchema.For(_constantType)` in the constructor; added the validation gate at the top of `Set<T>` and `Get<T>`
- `Tests/Editor/DSMSchemaValidationTests.cs` - New EditMode fixture; 8 `[Test]` methods covering the full RED spec from Task 1
- `Tests/Editor/DSMTestConfig.cs` - Added `strictSchema` parameter to `Create(...)`, mirroring the existing `SetField` pattern

## Decisions Made
- Reused the exact `DSMSlot.SeedDefaults` reflection target (`GetFields(BindingFlags.Public | BindingFlags.Static)` on the resolved `DSMConstant` type) for `DSMSchema`'s build path, so there is no second, separately-maintained key-type table (plan `must_haves.truths` criterion 3).
- Followed the plan's exact coercion recipe for lenient `Set`: `JToken.FromObject(value, serializer).ToObject(expected, serializer)`, re-wrapped via `JToken.FromObject` before storing; any exception in that round-trip is caught and the original value is stored unchanged (never throws in lenient mode).
- `Get<T>` intentionally does not attempt its own coercion/rewrite on mismatch — it warns and defers to the already-lenient `token.ToObject<T>(...) ?? defaultValue` read path per the plan's explicit instruction, avoiding duplicate coercion logic between Set and Get.

## Deviations from Plan

None — plan executed exactly as written. One environment-only adjustment (not a code deviation): the Unity-auto-generated, gitignored `DMS.Runtime.csproj` did not yet list the new `Runtime/DSMSchema.cs` file (Unity regenerates this file on domain reload, which cannot run here). Added the missing `<Compile Include>` line locally so `dotnet build DMS.Runtime.csproj` could verify the Runtime assembly compiles; this file is gitignored and was not committed.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Open Human-Verification Item (Unity Test Runner)

**This is the acceptance gate for SCHM-01/SCHM-02 and has NOT been run by this agent** — per `.claude/CLAUDE.md`, Unity tests (EditMode/PlayMode, batchmode, `-runTests`) must never be run by the executor; Unity batchmode has previously deadlocked this project and hit Unix-domain-socket path failures.

**What the human must do:**
1. Open Unity Editor -> Window -> General -> Test Runner -> EditMode tab.
2. Run the `DSMSchemaValidationTests` fixture (8 tests) and confirm all pass:
   - `Strict_SetTypeMismatch_ThrowsSchemaViolation`
   - `Strict_GetTypeMismatch_ThrowsSchemaViolation`
   - `Strict_MatchingType_DoesNotThrow`
   - `Strict_UnconstrainedKey_PassesThrough`
   - `Lenient_Mismatch_CoercesToSchemaType`
   - `Lenient_UncoercibleMismatch_DoesNotThrow`
   - `Strict_ExceptionMessage_DoesNotLeakOffendingValue`
   - `EmptySchema_NullConstantType_IsPassThroughNoOp`
3. Also re-run the full existing suite (Phase 1 + Phase 2 fixtures, e.g. `DSMEncryptionKeyTests`, `DSMKeyRotationTests`, and any Phase 1 fixtures) to confirm no regression from the new Set/Get validation gate.
4. Report back pass/fail; any failure should be treated as a bug against this plan's implementation (Task 2), not a plan deviation.

## Next Phase Readiness
`DSMSchema`, `DSMSchemaViolationException`, and `DSMConfig.StrictSchema` are available for any future phase that needs schema-aware behavior (e.g. widget/editor integration). No blockers for subsequent phases — the only open item is the human Unity Test Runner pass documented above, which does not block further planning/execution work but should be confirmed before this plan is considered fully verified end-to-end.

---
*Phase: 03-schema-validation*
*Completed: 2026-07-15*

## Self-Check: PASSED

- FOUND: Runtime/DSMSchema.cs
- FOUND: Tests/Editor/DSMSchemaValidationTests.cs
- FOUND: .planning/phases/03-schema-validation/03-01-SUMMARY.md
- FOUND commit: 63dc9a4
- FOUND commit: 7bd66c7
