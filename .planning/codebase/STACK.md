# Technology Stack

**Analysis Date:** 2026-07-08

## Languages

**Primary:**
- C# 11+ - All runtime and editor code in `Runtime/` and `Editor/` directories

## Runtime

**Environment:**
- Unity 6000.0 or later (as per `package.json`)

**Package Manager:**
- Unity Package Manager (UPM)
- Lockfile: `package.json` with git-based dependencies

## Frameworks

**Core:**
- Unity Engine - Base framework for serialization and editor integration
- UniTask 2.x - Async/await support and reactive streams (`IUniTaskAsyncEnumerable`)

**Testing:**
- Not detected

**Build/Dev:**
- Unity Editor C# compilation
- Assembly Definition files (`.asmdef`) for build partitioning:
  - `DMS.Runtime.asmdef` (`Runtime/`)
  - `DSM.Editor.asmdef` (`Editor/`)

## Key Dependencies

**Critical:**
- `com.cysharp.unitask` (git: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask) - Provides async/await primitives and `IUniTaskAsyncEnumerable` for reactive change watching in `Runtime/DSMWatcher.cs`
- `com.unity.nuget.newtonsoft-json` 3.2.1 - JSON serialization via Newtonsoft.Json for save file persistence (`Runtime/DSMSerializer.cs`)

**Infrastructure:**
- Unity built-in `System.Security.Cryptography` - AES-256 encryption with PBKDF2 in `Runtime/DSMEncryptor.cs`
- Unity built-in `System.IO` - File I/O for save slot persistence

## Configuration

**Environment:**
- No external environment variables required
- Encryption key set programmatically via `DSMConfig.SetEncryptionKey()` to prevent secrets in asset files

**Build:**
- `DMS.Runtime.asmdef`:
  - References: GUID entries for Unity built-in assemblies (JSON and core serialization)
  - All platforms included
  - No unsafe code
- `DSM.Editor.asmdef`:
  - References: DMS.Runtime assembly
  - Editor platform only
  - No unsafe code
- `package.json`:
  - Name: `com.draftsama.datasavemanager`
  - Version: 1.0.0
  - Unity minimum: 6000.0

## Platform Requirements

**Development:**
- Unity 6000.0+ with C# 11+ support
- .NET runtime compatible with Unity version

**Production:**
- Deployment to all platforms where Unity runs (PC, mobile, console)
- Persistent storage directory accessible via `UnityEngine.Application.persistentDataPath`

---

*Stack analysis: 2026-07-08*
