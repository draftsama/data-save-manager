# Codebase Concerns

**Analysis Date:** 2026-07-08

## Tech Debt

**Large Editor Window Class:**
- Issue: `DSMManagerWindow.cs` is 825 lines in a single class. Contains UI rendering, reflection-based data loading, slot management, entry editing, and code generation all mixed together. This makes the code difficult to maintain and test.
- Files: `Editor/DSMManagerWindow.cs`
- Impact: Changes to any part of the UI or data flow require navigating a large file. Risk of regressions when modifying features. Difficult to understand control flow.
- Fix approach: Split into smaller, focused classes (e.g., `DSMManagerWindowUI.cs` for rendering, `DSMDefaultsManager.cs` for reflection/syncing, `DSMSlotController.cs` for slot operations). Consider extracting style definitions and GUI constants to a separate file.

**Broad Exception Handling:**
- Issue: `DSMManagerWindow.cs` line 212 uses `catch { return null; }` to swallow all exceptions when reading slot data. This hides decryption failures, file I/O errors, and JSON parsing errors.
- Files: `Editor/DSMManagerWindow.cs` (lines 202-213)
- Impact: Silent failures make debugging difficult. If decryption fails (wrong key), the window appears to work but shows no data. User cannot distinguish between "no save file" and "decryption error."
- Fix approach: Catch specific exceptions, log details, and display user-friendly errors in the UI. At minimum: `catch (InvalidOperationException)` for decryption, `catch (FileNotFoundException)`, `catch (JsonReaderException)`.

**Encryption Key Validation:**
- Issue: No validation for encryption key. Empty string is used as fallback in multiple places (`DSMManagerWindow.cs` line 206, `DSMSlot.cs` lines 59, 74, 87). This allows encryption to proceed with an empty key or decryption with a missing key without explicit failure.
- Files: `Runtime/DSMSlot.cs` (lines 59, 74, 87), `Editor/DSMManagerWindow.cs` (line 206)
- Impact: If encryption is enabled but the key is not set, saves are encrypted with an empty key, making them unrecoverable if the key is later changed. Decryption with empty fallback produces silent failures.
- Fix approach: Add validation in `DSMConfig.SetEncryptionKey()` to reject empty/short keys. In `DSMSlot.Save()` and `DSMSlot.Load()`, throw a clear exception if `Encrypt` is true but `EncryptionKey` is empty. Update `DSMManagerWindow` to fail visibly if key is missing.

**Missing Slot Name Validation:**
- Issue: Slot names are used directly in file paths (`DSMSlot.cs` line 162, `DSMSlotManager.cs` lines 39-40) without validation. Malicious or accidental slot names like `"../../../etc/passwd"` or `"con.json"` (Windows reserved names) could cause issues.
- Files: `Runtime/DSMSlot.cs`, `Runtime/DSMSlotManager.cs`, `Runtime/DSMPaths.cs`
- Impact: Path traversal attacks in multiplayer or user-input scenarios. Windows reserved device names cause I/O failures.
- Fix approach: Add slot name validation in `DSMSlotManager.UseSlot()` and `GetOrCreateSlot()`. Reject names containing path separators (`/`, `\`), dots (`..`), Windows reserved names (`CON`, `PRN`, `AUX`, etc.), and non-alphanumeric characters except underscore/hyphen.

## Known Bugs

**Silent Widget Component Errors:**
- Symptoms: Exposed entries in Runtime Canvas show no widgets, but no error is logged.
- Files: `Runtime/DSMRuntimePanel.cs` (line 44)
- Trigger: Widget prefab is missing the `IDSMWidget` component, or `DSMWidgetConfig` doesn't have a prefab for the entry type.
- Workaround: Check the console for the initial log error on `Awake()` (lines 26-30). Inspect `DSMWidgetConfig` in the editor to ensure all types have prefab assignments.
- Fix approach: Log a warning when a widget prefab is instantiated but lacks `IDSMWidget`. Add a validation method in `DSMRuntimePanel` to check prefab setup before instantiation.

## Security Considerations

**Encryption with Empty Key:**
- Risk: If `DSMConfig.Encrypt` is enabled but no key is set via `SetEncryptionKey()`, saves are encrypted with an empty string. This is not cryptographically secure and provides no real protection.
- Files: `Runtime/DSMSlot.cs`, `Runtime/DSMConfig.cs`, `Runtime/DSMEncryptor.cs`
- Current mitigation: README warns to set encryption key in `Awake()` before any save/load, but there is no programmatic enforcement.
- Recommendations: 
  - Throw `InvalidOperationException` in `DSMSlot.Save()` and `DSMSlot.Load()` if `Encrypt == true` and `EncryptionKey.Length < 16`.
  - Add a property `bool HasEncryptionKey { get; }` to `DSMConfig` for runtime checks.
  - Update example code in README to show key validation.

**Missing Input Validation for Entry Keys:**
- Risk: Entry keys from `DSMConstant` reflection are not validated. A malformed constant field name could create invalid JSON keys or code generation issues.
- Files: `Editor/DSMManagerWindow.cs` (lines 114-125), `Editor/DSMCodeGenerator.cs` (lines 26-32)
- Current mitigation: Code generator warns if key starts with lowercase (line 30), but does not reject it.
- Recommendations: Enforce strict key validation: alphanumeric + underscore, starting with uppercase. Reject special characters, spaces, and empty strings. Fail the code generation if validation fails.

## Performance Bottlenecks

**Reflection Scan on Every Window Open:**
- Problem: `DSMManagerWindow.LoadDefaultsFromReflection()` scans all assemblies and types on every window reload, including when the window is opened or the editor recompiles.
- Files: `Editor/DSMManagerWindow.cs` (lines 92-125)
- Cause: Full assembly/type scan is O(n) over the number of types. With large projects, this can take hundreds of milliseconds.
- Improvement path: Cache the `DSMConstant` type after first discovery. Invalidate cache only on code recompile (hook into `UnityEditor.Compilation.CompilationPipeline.compilationStarted`).

**GetAllSlots() Uses Directory.GetFiles() on Every Slot Load:**
- Problem: `DSMSlotManager.GetAllSlots()` and `DSMManagerWindow.DiscoverSlots()` scan the save directory repeatedly during normal operation.
- Files: `Runtime/DSMSlotManager.cs` (lines 45-61), `Editor/DSMManagerWindow.cs` (lines 158-174)
- Cause: No caching; every call to `GetAllSlots()` does I/O.
- Improvement path: Cache the slot list in `DSMSlotManager` and invalidate only when a slot is created/deleted. In the Editor window, refresh the slot list only on manual "Refresh" or after a slot operation, not on every draw.

## Fragile Areas

**Thread Safety of DSMSlot._data:**
- Files: `Runtime/DSMSlot.cs` (lines 20, 32-50)
- Why fragile: The `_data` dictionary is accessed by `Set()`, `Get()`, `Load()`, `Save()` without synchronization. If `LoadAsync()` is called while `Set()` is modifying `_data`, data corruption or `KeyNotFoundException` can occur.
- Safe modification: Add a lock around all `_data` access, or use `ConcurrentDictionary<string, JToken>`. Ensure `ScheduleSave()` doesn't race with explicit `Save()`/`SaveAsync()` calls.
- Test coverage: No tests visible for concurrent `Set()` + `Load()` or `Set()` + `SaveAsync()` scenarios.

**Debounce Timer Disposal in ScheduleSave():**
- Files: `Runtime/DSMSlot.cs` (lines 136-156)
- Why fragile: When `ScheduleSave()` is called, the old `CancellationTokenSource` is canceled and disposed immediately. If `DebouncedSaveAsync()` tries to use the token after disposal, it could throw. Also, rapid calls to `ScheduleSave()` could create and dispose many CTS instances.
- Safe modification: Use `CancellationToken.ThrowIfCancellationRequested()` checks inside the debounce task. Consider using `System.Threading.Timer` or `UniTask.Delay` with a single active debounce task instead of creating new CTS every call.
- Test coverage: No visible tests for rapid `Set()` calls with `AutoSave` enabled.

**Hardcoded Output Path in DSMCodeGenerator:**
- Files: `Editor/DSMCodeGenerator.cs` (line 13)
- Why fragile: The output path `"Assets/DataSaveManager/Runtime/DSMConstant.cs"` is hardcoded. If the package is moved to a different path, code generation will fail silently (line 39 checks if content is identical, so no write happens). A developer moving the package will get no error.
- Safe modification: Make the output path configurable via `DSMConfig`, or resolve it dynamically based on the location of the `DSMCodeGenerator` script itself using `AssetDatabase.GetAssetPath()`.
- Test coverage: No visible tests for code generation with non-standard package paths.

## Scaling Limits

**In-Memory Slot Cache:**
- Current capacity: No explicit limit. All loaded slots remain in memory in `DSMSlotManager._slots`.
- Limit: If a game creates hundreds of unique slots (e.g., per-user-id slots), memory grows unbounded. No automatic unloading or LRU eviction.
- Scaling path: Add a configuration option for maximum cached slots. Implement LRU eviction in `DSMSlotManager`. Provide `UnloadSlot(name)` method to manually free memory.

**Runtime Widget Instantiation:**
- Current capacity: `DSMRuntimePanel.BuildWidgets()` instantiates one GameObject per exposed entry. No batching or pooling.
- Limit: If `DSMConfig.ExposedEntries` has thousands of entries, the UI will be unresponsive.
- Scaling path: Implement a scroll-based lazy loading system. Pre-create a fixed pool of widget instances and recycle them.

## Dependencies at Risk

**UniTask Git Dependency:**
- Risk: `package.json` line 11 specifies UniTask as a git dependency with a specific path: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`. This works if the repository structure remains stable, but any major refactoring of the UniTask repo could break the dependency.
- Impact: Build failures if UniTask repo moves or restructures. No version pinning; always pulls latest `main` branch.
- Migration plan: Switch to the UPM package if available, or pin to a specific commit hash in the git URL (e.g., `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#<commit-hash>`).

**Newtonsoft.Json Version:**
- Risk: `package.json` line 12 specifies `com.unity.nuget.newtonsoft-json` version `3.2.1`. This is a stable version, but older than the latest available.
- Impact: Missing bug fixes and security patches. Serialization/deserialization edge cases might differ in newer versions.
- Migration plan: Test and update to the latest 3.x version quarterly. Monitor the Newtonsoft.Json changelog for breaking changes.

## Missing Critical Features

**No Data Validation or Schema:**
- Problem: DSM accepts any serializable type without schema validation. A key set with type `int` can be overwritten with a `string`, and subsequent `Get<int>()` calls will fail silently.
- Blocks: Type-safe data management. Cannot prevent accidental type mismatches.
- Fix approach: Add a schema system where `DSMConstant` defines not just the key and default, but also the expected type. Validate on `Set()` and `Get()` that types match, or implement a type coercion strategy.

**No Slot Versioning or Migration:**
- Problem: If the structure of `DSMConstant` changes (e.g., a key is renamed or a default value changes), old save files cannot be auto-migrated. Manual version bumps and migration logic are required.
- Blocks: Automatic save file updates across app versions. Cannot remove keys without breaking old saves.
- Fix approach: Add a `SaveVersion` field to `DSMConfig`. Track version in save file headers. Provide a migration callback interface to transform data on load.

**No Async Watchers:**
- Problem: `DSM.WatchAsync<T>()` streams changes, but if many watchers are active and `Set()` is called, all watchers are notified synchronously. High change frequency can cause frame rate drops.
- Blocks: Efficient reactive UI updates in performance-critical scenarios.
- Fix approach: Batch notifications via a deferred queue. Apply changes once per frame or after a debounce period.

**No Encryption Key Rotation:**
- Problem: Once data is encrypted with a key, there is no way to change the encryption key without manually re-encrypting all save files.
- Blocks: Security best practices (periodic key rotation). Player data recovery if key is compromised.
- Fix approach: Store encryption key version in save file header. Implement a re-encrypt operation that loads with old key and saves with new key.

## Test Coverage Gaps

**Untested Encryption Edge Cases:**
- What's not tested: Decryption with wrong key, truncated encrypted files, encryption with empty key, key changes between saves/loads.
- Files: `Runtime/DSMEncryptor.cs`, `Runtime/DSMSlot.cs` (encrypt/decrypt paths)
- Risk: Encryption logic could silently fail or produce corrupted data without detection. End-user data loss risk.
- Priority: High

**Untested Concurrent Slot Operations:**
- What's not tested: Concurrent `Set()` + `Load()`, `SaveAsync()` + `Get()`, multiple watchers + rapid `Set()` calls.
- Files: `Runtime/DSMSlot.cs`, `Runtime/DSMWatcher.cs`
- Risk: Race conditions and data corruption in concurrent scenarios. Potential crashes with `KeyNotFoundException` or `NullReferenceException`.
- Priority: High

**Untested Editor Window State Management:**
- What's not tested: Switching slots while editing, rapid refresh cycles, deleting slots during slot selection, exception handling in reflection/JSON parsing.
- Files: `Editor/DSMManagerWindow.cs`
- Risk: Editor crashes, data loss (e.g., deleting slot while it's selected), UI state corruption.
- Priority: Medium

**No Tests for Invalid Inputs:**
- What's not tested: Null keys, empty slot names, malformed JSON in save files, missing widget components, invalid type conversions.
- Files: All runtime and editor files
- Risk: Crashes or silent failures with invalid inputs. Poor error messages.
- Priority: Medium

**Breaking Change Migration Not Tested:**
- What's not tested: Loading v0.x encrypted files with v1.0 reader (mentioned in README line 388: "Files encrypted with the previous format (fixed salt) cannot be decrypted").
- Files: `Runtime/DSMEncryptor.cs`
- Risk: Existing users with old encrypted saves will lose access to their data. No migration path documented or tested.
- Priority: High (if users exist with old saves)

---

*Concerns audit: 2026-07-08*
