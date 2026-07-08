# Stack Research

**Domain:** Unity save-system hardening (encryption, versioning, validation, concurrency, testing)
**Researched:** 2026-07-08
**Confidence:** MEDIUM-HIGH (mixed — see per-item confidence; platform-support claims verified against official Microsoft docs)

This is **not** a greenfield stack — DSM already runs on Unity 6000.0+, C# 11+, UniTask, and `com.unity.nuget.newtonsoft-json` 3.2.1 (see `.planning/codebase/STACK.md`). Every recommendation below stays inside that stack. No new runtime dependencies are recommended; hardening is achieved with .NET BCL primitives already available to Unity plus Unity's own bundled test framework.

## Recommended Stack

### Core Technologies (hardening additions — no new packages)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `System.Security.Cryptography.Aes` (CBC) + `System.Security.Cryptography.HMACSHA256` | BCL, already referenced | Authenticated encryption (Encrypt-then-MAC) for save files | AES-GCM is the modern default for authenticated encryption **but is officially unsupported on WebGL/browser** and has historically inconsistent IL2CPP support on iOS/tvOS (see Pitfalls). DSM's asmdef targets "all platforms," so a fully-managed EtM construction (existing AES-CBC + a new HMAC-SHA256 tag) is the only authenticated-encryption approach guaranteed to work everywhere Unity runs, including WebGL. Confidence: HIGH (browser exclusion verified directly against Microsoft's official `AesGcm` API reference). |
| `System.Security.Cryptography.Rfc2898DeriveBytes` (instance constructor, already used) | BCL, already referenced | PBKDF2 key derivation from passphrase | Already in `DSMEncryptor.cs`. Keep the instance-based constructor (`new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)`) rather than assuming the newer static `Rfc2898DeriveBytes.Pbkdf2(...)` helper (added .NET 7) is available — Unity's IL2CPP BCL subset compatibility for that overload is unverified. The instance API is proven to work in this codebase today. Confidence: HIGH (already running in production). |
| NUnit (via `com.unity.test-framework`, Unity-bundled) | Matches Editor version (Unity 6000.x ships Test Framework in the 1.4–1.6 line; exact patch is locked to the Editor build, not independently pinned) | Edit Mode + Play Mode test runner | This is Unity's only first-party test framework and is bundled with every Editor install — "Core packages are fixed to a single version matching the Editor version" per Unity's own package docs. No alternative test runner is standard in the Unity ecosystem. Confidence: MEDIUM (exact version number for Unity 6000.0 not independently confirmed; the *fact* that it's Editor-locked and NUnit-based is HIGH confidence, official Unity docs). |

### Supporting Libraries — none to add

| Library | Verdict | Why Not Needed |
|---------|---------|-----------------|
| `Newtonsoft.Json.Schema` | Do not add | Commercial, per-developer licensed product with a rate-limited free tier (not viable to ship inside an open package). DSM's validation need is narrow (type-check `JToken` against an expected CLR type, validate a version envelope) — a ~30-line hand-rolled validator using the `JToken.Type` you already get from Newtonsoft is simpler, free, and has zero new attack surface. |
| `JsonSchema.Net` (json-everything) | Do not add | Built for `System.Text.Json`, not Newtonsoft — pulling it in means carrying two JSON stacks side by side for a package whose whole serialization layer is Newtonsoft-based (`DSMSerializer.cs`). Not worth the weight for slot/schema validation this narrow. |
| `FluentValidation` | Do not add | Designed for validating rich DTOs/forms with many interdependent rules. DSM's validation surface is "does this JToken's runtime type match the constant's declared type" and "is `schemaVersion` known" — a switch/dictionary-based check is more legible to future maintainers than a fluent rule-builder DSL for two checks. |
| `System.Collections.Concurrent.ConcurrentDictionary` | Prefer plain `lock` instead (see Architecture item) | Technically available under IL2CPP, but there are multiple independent reports of `ConcurrentDictionary` enumeration/iteration issues under IL2CPP AOT (`GetEnumerator().Reset()` throwing `NotSupportedException`; sporadic crashes reported in unrelated IL2CPP projects). DSM's `_data` dictionary is enumerated wholesale on every save (serialize-to-JSON), which is exactly the risky access pattern. A `lock`-guarded plain `Dictionary<string, JToken>` is simpler, has no such reports, and DSM's access pattern (coarse-grained get/set/serialize, not high-throughput parallel writers) doesn't need lock-free structures. Confidence: MEDIUM (IL2CPP issues are anecdotal forum/GitHub reports, not an official Unity limitation — but there's no upside to risking it here). |
| `Moq` / `NSubstitute` | Avoid in Play Mode test assemblies; optional in Editor-only Edit Mode assemblies | Both rely on runtime dynamic-proxy code generation, which is fragile-to-broken under IL2CPP AOT (device Play Mode builds). DSM's public surface (`DSM`, `DSMSlot`, `DSMEncryptor`) is concrete static/sealed classes, not built around interfaces for DI — there's little to mock. Test against real file I/O in a temp directory (`Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString())`) instead; it's more representative of real save/load behavior anyway. |
| `FluentAssertions` / `Shouldly` | Not needed | NUnit's built-in `Assert.That(...)` / `Is` constraint model is sufficient for this package's test scope and avoids an extra dependency + license terms (FluentAssertions went commercial for some use in 2025) to track. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `com.unity.test-framework` | Edit Mode + Play Mode test execution | Already implicitly available in any Unity 6000.x project (bundled default package) — the DSM package should still declare it explicitly in `package.json` `dependencies` so consumers embedding/cloning the package get a clear signal it's expected. |
| Two new asmdefs: `Tests/Editor/DSM.Tests.Editor.asmdef`, `Tests/Runtime/DSM.Tests.Runtime.asmdef` | Isolate test code from shipped runtime/editor assemblies | Standard Unity pattern — test assemblies must NOT be referenced by `DMS.Runtime.asmdef` or `DSM.Editor.asmdef` (that would ship NUnit references in the package's production code). See Architecture Patterns below for exact asmdef fields. |

## Installation

No new packages. `package.json` gains one explicit dependency declaration (the package is already implicitly present in any Unity 6000 project, but declaring it documents the requirement and lets UPM's dependency resolver be explicit about it):

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.test-framework": "1.4.5"
  }
}
```

Do not hand-pin an exact `com.unity.test-framework` version beyond what's needed to satisfy UPM's manifest resolution — Unity locks the actual resolved version to the Editor build regardless of what's declared, so treat the declared version as a minimum floor, not a hard pin. Verify the exact bundled version in your target Unity 6000.x Editor via `Window > Package Manager > Packages: Unity Registry > Test Framework` before finalizing the floor version in `package.json`.

No `Tests.meta` GUID conflicts: create the `Tests/` folder at the package root (sibling to `Runtime/` and `Editor/`), not nested inside either, so its asmdefs can independently target Editor-only vs. all-platforms.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| AES-CBC + HMAC-SHA256 (Encrypt-then-MAC) | AES-GCM (`System.Security.Cryptography.AesGcm`) | If you drop WebGL support entirely AND can confirm (via `AesGcm.IsSupported` guarded at runtime, with a graceful fallback) that every target platform's IL2CPP build actually supports it. GCM is simpler to implement correctly (no separate MAC step, no length-extension concerns) and slightly faster — but the platform guarantee isn't there for a package that ships to "all platforms." |
| Hand-rolled version-envelope + migration-callback chain | A general migration framework (e.g., patterns borrowed from `FluentMigrator`) | Only relevant if DSM's save format grows to relational/multi-table complexity. For a single JSON blob per slot, a `schemaVersion` int + ordered list of `IDSMMigration.Apply(JObject)` transforms is simpler and has zero new dependencies. |
| Plain `Dictionary` + `lock` | `ConcurrentDictionary` | If profiling on your actual target platforms shows lock contention is a real bottleneck (unlikely for a save system driven by user actions, not a hot per-frame loop) AND you've verified `ConcurrentDictionary` enumeration is safe on your specific IL2CPP/platform combination. |
| NUnit (bundled) | None — no viable alternative in Unity ecosystem | N/A — this is a solved/only choice, not a comparison. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| `AesGcm` if WebGL is a supported/possible target platform | Microsoft's own API reference marks `AesGcm` with `[UnsupportedOSPlatform("browser")]` unconditionally across all current .NET versions — this is a permanent platform exclusion, not a version-gap that will close. Throws `PlatformNotSupportedException` at construction time on WebGL. | AES-CBC + HMAC-SHA256 (Encrypt-then-MAC), fully managed, no native platform dependency. |
| Raw `CancellationTokenSource` churn on every `ScheduleSave()` call (current code) | Creating + immediately canceling + disposing a new CTS on every debounced `Set()` call is a known source of `ObjectDisposedException` races if a prior delay task reads the token after disposal — exactly the bug flagged in CONCERNS.md. | A single long-lived debounce loop: bump a `_generation` counter on each `Set()`, `await UniTask.Delay(debounceMs)`, then check `if (generation == _generation) Save();`. No CTS lifecycle to race. |
| Empty-string encryption key silently accepted (current code) | Encrypts with a trivially guessable key derivation, giving a false sense of security; also makes future key-rotation logic ambiguous ("was this slot ever really encrypted?"). | Throw `InvalidOperationException` in `DSMConfig.SetEncryptionKey()` and again defensively in `DSMSlot.Save()`/`Load()` if `Encrypt == true` and key length is below a documented minimum (16 chars, matching the AES-256 key material recommendation already implied by `KeySize = 256` in `DSMEncryptor`). |
| `Newtonsoft.Json.Schema` for save-file validation | Commercial license with hourly quota on the free tier — inappropriate for logic shipped inside a reusable package other projects will consume. | Hand-rolled `JToken.Type` vs. expected `System.Type` check, backed by the type metadata DSM's code generator already captures per `DSMConstant` entry. |
| Mocking frameworks (Moq/NSubstitute) in `Tests/Runtime` (Play Mode) assemblies | Dynamic proxy codegen is unreliable under IL2CPP AOT compilation used in device Play Mode test runs. | Real file I/O against a temp directory per test, cleaned up in teardown. DSM's surface is small enough that this is not meaningfully slower. |

## Stack Patterns by Variant

**If DSM must support WebGL (browser) as a target platform:**
- AES-GCM is a hard no (`UnsupportedOSPlatform("browser")`, verified against Microsoft's official API docs).
- Use AES-CBC + HMAC-SHA256 unconditionally; don't runtime-branch by platform — one code path is simpler to test and audit than two encryption implementations.

**If DSM will never target WebGL and iOS/tvOS behavior is verified acceptable:**
- AES-GCM becomes viable, but only behind an explicit `AesGcm.IsSupported` check with the EtM path as a fallback — do not assume support based on .NET version alone, since Unity's IL2CPP BCL subset does not track upstream .NET .NET release notes 1:1.
- This is a "nice to have, not required" optimization; EtM is already secure and simpler to reason about, so there's limited incentive to add the branch unless GCM's smaller ciphertext (no padding) or throughput matters for your save sizes.

**If encryption key rotation needs to support "rotate while old saves still exist on disk":**
- Add a 1-byte `KeyVersion` to the save file header (alongside the existing `[16-byte IV][32-byte salt]` prefix already in `DSMEncryptor`).
- `DSMConfig` should track a *current* key plus, transiently during a rotation operation, the *previous* key — not an open-ended key history. On `Load()`, if the header's `KeyVersion` doesn't match current, decrypt with the previous key, then re-encrypt with the current key and persist immediately (lazy migration on read), or expose an explicit `DSM.RotateSlotKeyAsync(slotName, oldKey, newKey)` that does it eagerly for all slots. Given "no backward compatibility required for old saves" is already an accepted constraint for this milestone, eager rotation (no dual-key fallback state kept around indefinitely) is the simpler, recommended approach — don't build a general N-generation key history for a single-player local save file; that complexity belongs to server-side/KMS-backed systems, not this package.

**If save-file schema changes over the game's lifetime (fields added/removed/renamed):**
- Wrap the serialized payload in an envelope: `{ "schemaVersion": N, "data": { ...existing per-key JSON... } }`.
- On load, if `schemaVersion < DSMConfig.CurrentSchemaVersion`, run each `IDSMMigration` in the registered chain (`v1→v2`, `v2→v3`, ...) against the `JObject` before deserializing into typed values. Each migration is a small, independently testable transform — this is the same shape used by EF Core migrations and most local-first apps, just without an ORM.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| `com.unity.test-framework` (Edit/Play Mode) | Unity 6000.0+ | Bundled/version-locked to the Editor; declare in `package.json` as a floor version, verify actual resolved version in your Editor's Package Manager window before relying on any specific `[UnityTest]`/async-test API surface. |
| `async Task` test methods (`[Test] public async Task ...`) | Unity 2023.1+ / Unity 6000.x | Supported natively by Test Framework — no UniTask bridging needed for plain `Task`-based async tests. |
| UniTask-based async code under test (`UniTask`, not `Task`) | Requires `[UnityTest] IEnumerator` + `.ToCoroutine()` | UniTask methods are not directly awaitable by NUnit's `async Task` test runner path the same way `Task` is — bridge via `SomeUniTaskMethod().ToCoroutine()` inside a `[UnityTest]` for Play Mode tests that exercise `DSMSlot.SaveAsync()`/`LoadAsync()`/`DSMWatcher.WatchAsync()`. |
| `System.Security.Cryptography.Aes` (CBC, existing) | All Unity platforms incl. WebGL, IL2CPP, Mono | Fully managed on all Unity scripting backends — this is why it's the safe baseline to build EtM on top of, unlike `AesGcm`. |
| `HMACSHA256` (new addition) | All Unity platforms | Same managed BCL surface as `Aes`; no platform exclusions found in official docs. |

## Sources

- `learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm` — fetched directly, official Microsoft API reference; confirmed `[UnsupportedOSPlatform("browser")]` attribute on `AesGcm` class across current monikers. Confidence: HIGH (primary source, fetched not summarized from memory).
- `github.com/dotnet/runtime` issue #92482 ("Algorithm 'AesGcm' is not supported on this platform but platform is supported") — corroborates real-world iOS/tvOS `AesGcm` support inconsistency even where `IsSupported` reports true. Confidence: MEDIUM (GitHub issue, cross-referenced with Unity Discussions reports of the same symptom under IL2CPP).
- Unity Discussions: "Can't use CryptoStream in IL2CPP builds" — corroborating anecdote for IL2CPP crypto backend quirks generally. Confidence: LOW-MEDIUM (forum report, not official Unity documentation) — used only as directional signal, not as the basis for a hard recommendation on its own.
- OWASP Password Storage Cheat Sheet (via web search summary, current guidance) — PBKDF2-HMAC-SHA256 recommended at 600,000 iterations as of the 2023+ revision (up from the historical 100k figure), target ~100ms derivation time on production hardware. Confidence: MEDIUM (web search, not directly fetched from owasp.org in this pass — verify iteration count against the live OWASP cheat sheet before hardcoding, and benchmark on your actual minimum-spec target device since mobile CPUs are far slower than the reference hardware used to set that guidance).
- `docs.unity3d.com/Packages/com.unity.test-framework@*/manual/edit-mode-vs-play-mode-tests.html` and `.../reference-async-tests.html` — official Unity Test Framework manual pages, confirm Edit Mode asmdef must be Editor-only + reference `nunit.framework.dll`, Play Mode asmdef must reference `UnityEngine.TestRunner`, and native `async Task` test support ships in Unity 2023.1+/Unity 6000.x. Confidence: MEDIUM (web-search-summarized rather than directly fetched in full; core setup facts are consistent with long-standing, stable Unity documentation).
- `github.com/needle-mirror/com.unity.test-framework` releases — used to sanity-check that Test Framework versions are actively maintained (1.4.x/1.7.x/2.0.x-exp lines observed); exact version bundled with a specific Unity 6000.0 Editor build was not independently confirmed in this pass — verify locally. Confidence: LOW-MEDIUM on the exact version number only; the general "core package, Editor-locked version" fact is HIGH confidence (stated directly on Unity's own package manual page).
- General web search cross-verification (ConcurrentDictionary vs lock, Newtonsoft.Json.Schema licensing, JsonSchema.Net ecosystem fit) — MEDIUM confidence per standard web-search cross-checking; no single claim here rests on an unverifiable single source, but none were fetched from a single canonical spec page either.

---
*Stack research for: Unity save-system hardening (DataSaveManager v1.0.0 → hardening milestone)*
*Researched: 2026-07-08*
