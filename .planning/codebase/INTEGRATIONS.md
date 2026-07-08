# External Integrations

**Analysis Date:** 2026-07-08

## APIs & External Services

**None detected.**

This package does not integrate with external APIs or cloud services. All data persists locally.

## Data Storage

**Local Filesystem Only:**
- Default save location: `{Application.persistentDataPath}/DSM/`
- Configurable via `DSMConfig.SavePath` property
- Storage format: JSON (`.json` extension) or encrypted binary (`.enc` extension)
- Single-file per slot architecture (e.g., `default.json`, `slot2.json`)
- `Runtime/DSMSlot.cs` handles file I/O synchronously and asynchronously
- `Runtime/DSMPaths.cs` centralizes path resolution

**File Storage:**
- Local filesystem only - no cloud storage integration

**Caching:**
- In-memory: `DSMSlot` maintains a `Dictionary<string, JToken>` for active slot data
- No external cache layers (Redis, Memcached, etc.)

## Authentication & Identity

**Auth Provider:**
- None - package is standalone with no user identity system
- Encryption key managed programmatically in code (not in assets)

**Encryption:**
- AES-256-CBC with PBKDF2 key derivation (10,000 iterations)
- Per-file random salt (32 bytes) prepended to ciphertext to prevent rainbow-table attacks
- IV (16 bytes) prepended before salt
- Format: `[16-byte IV][32-byte salt][ciphertext]`
- Implemented in `Runtime/DSMEncryptor.cs`
- Key derivation uses `System.Security.Cryptography.Rfc2898DeriveBytes`

## Monitoring & Observability

**Error Tracking:**
- None detected

**Logs:**
- None detected - package uses no logging framework
- Code generation warnings logged via `Debug.LogWarning()` in `Editor/DSMCodeGenerator.cs`

## CI/CD & Deployment

**Hosting:**
- Not applicable - this is a package, not a deployed application
- Distributed via git submodule or UPM package registry

**CI Pipeline:**
- None detected

## Environment Configuration

**Required env vars:**
- None

**Secrets location:**
- Encryption key: Set at runtime via `config.SetEncryptionKey("key-string")` in user code
- Not stored in `DSMConfig.asset` to prevent secrets in version control
- Example in README: Awake-time initialization pattern recommended

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

## Runtime Integration Points

**Unity Lifecycle:**
- `Application.quitting` - Automatic save of active slot on exit (registered in `DSM.Initialize()`)
- `ScriptableObject.CreateInstance<DSMConfig>()` - Lazy fallback config creation
- `Resources.Load<DSMConfig>("DSMConfig")` - Config asset loading from Resources folder
- `UnityEngine.UI` integration via `DSMRuntimePanel` for in-game widget rendering

**Storage Modes:**
- Synchronous: `DSMSlot.Save()` / `DSMSlot.Load()`
- Asynchronous: `DSMSlot.SaveAsync()` / `DSMSlot.LoadAsync()` via UniTask

---

*Integration audit: 2026-07-08*
