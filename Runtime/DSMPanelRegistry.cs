#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DataSaveManager
{
    /// <summary>The Runtime Panels the app created from the Config's Panel Definitions, found by their Panel Definition's name.</summary>
    public sealed class DSMPanelRegistry
    {
        private readonly List<DSMRuntimePanel> _panels = new();
        private readonly HashSet<string> _warnedNames = new(StringComparer.Ordinal);
        private readonly List<EventSystem> _sceneEventSystems = new();
        private readonly DSMLockTurn _lockTurn = new();
        private GameObject? _createdEventSystem;

        public IReadOnlyList<DSMRuntimePanel> All => _panels;

        /// <summary>True once the Runtime Panels were created, even if no Panel Definition was enabled.</summary>
        public bool HasSpawned { get; private set; }

        /// <summary>The first Runtime Panel whose Panel Definition is named <paramref name="name"/>.</summary>
        public bool TryGet(string name, out DSMRuntimePanel panel)
        {
            foreach (var p in _panels)
            {
                if (p != null && string.Equals(p.Name, name, StringComparison.Ordinal))
                {
                    panel = p;
                    return true;
                }
            }
            panel = null!;
            return false;
        }

        /// <summary>Like <see cref="TryGet"/>, but returns null and logs one warning per unknown name.</summary>
        public DSMRuntimePanel? Get(string name)
        {
            if (TryGet(name, out var panel)) return panel;
            if (_warnedNames.Add(name))
                Debug.LogWarning($"DSM: no Runtime Panel named '{name}'. Check the Panel Definitions in the DSM Manager.");
            return null;
        }

        public void HideAll()
        {
            foreach (var panel in _panels)
                if (panel != null) panel.Hide();
        }

        internal void Remove(DSMRuntimePanel panel)
        {
            _panels.Remove(panel);
            _lockTurn.Release(panel, Time.frameCount);
        }

        /// <summary>Asks to show <paramref name="panel"/>'s Lock Screen; false when another panel's Lock Screen has the turn.</summary>
        internal bool ClaimLockScreen(DSMRuntimePanel panel)
        {
            var order = _panels.IndexOf(panel);
            if (order < 0) order = int.MaxValue;
            var result = _lockTurn.Claim(panel, order, Time.frameCount, out var displaced);
            if (result == DSMLockTurnResult.TakeOver && displaced is DSMRuntimePanel other && other != null)
                other.DropLockScreen();
            return result != DSMLockTurnResult.Yield;
        }

        internal void ReleaseLockScreen(DSMRuntimePanel panel) => _lockTurn.Release(panel, Time.frameCount);

        internal void Clear()
        {
            _panels.Clear();
            _warnedNames.Clear();
            _lockTurn.Clear();
            HasSpawned = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Destroys the Runtime Panels and creates them again from <paramref name="config"/>.</summary>
        internal void Respawn(DSMConfig config)
        {
            foreach (var panel in _panels)
            {
                if (panel == null) continue;
                // Deactivated first so the old panel's Open Triggers stop before Destroy takes effect at the end of the frame.
                panel.gameObject.SetActive(false);
                Object.Destroy(panel.gameObject);
            }
            Clear();
            SpawnAll(config);
        }

        internal void SpawnAll(DSMConfig config)
        {
            HasSpawned = true;
            var spawnedAny = false;
            foreach (var definition in config.Panels)
            {
                if (!definition.Enabled) continue;
                if (definition.Prefab == null)
                {
                    Debug.LogError($"DSM: Panel Definition '{definition.Name}' has no prefab, so no Runtime Panel was created for it.");
                    continue;
                }
                _panels.Add(Spawn(definition));
                spawnedAny = true;
            }

            if (!spawnedAny) return;
            EnsureEventSystem();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static DSMRuntimePanel Spawn(DSMPanelDefinition definition)
        {
            // Parented under an inactive holder so Awake waits until the panel has its Panel Definition.
            var holder = new GameObject("DSM Panel Holder");
            holder.SetActive(false);
            var panel = Object.Instantiate(definition.Prefab!, holder.transform);
            panel.name = $"DSM Panel ({definition.Name})";
            panel.Initialize(definition);
            panel.transform.SetParent(null, false);
            Object.DontDestroyOnLoad(panel.gameObject);
            Object.Destroy(holder);
            return panel;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_createdEventSystem != null && SceneHasEventSystem(scene))
            {
                Object.Destroy(_createdEventSystem);
                _createdEventSystem = null;
            }
            EnsureEventSystem();
        }

        private bool SceneHasEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(true, _sceneEventSystems);
                foreach (var eventSystem in _sceneEventSystems)
                {
                    if (eventSystem.gameObject == _createdEventSystem) continue;
                    _sceneEventSystems.Clear();
                    return true;
                }
            }
            _sceneEventSystems.Clear();
            return false;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null || _createdEventSystem != null) return;
            _createdEventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            _createdEventSystem.AddComponent<InputSystemUIInputModule>();
#else
            _createdEventSystem.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
