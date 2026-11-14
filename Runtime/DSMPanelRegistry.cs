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

        public IReadOnlyList<DSMRuntimePanel> All => _panels;

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

        internal void Remove(DSMRuntimePanel panel) => _panels.Remove(panel);

        internal void Clear()
        {
            _panels.Clear();
            _warnedNames.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        internal void SpawnAll(DSMConfig config)
        {
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

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureEventSystem();

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
