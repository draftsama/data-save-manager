#nullable enable

using System.Linq;
using UnityEngine;

namespace DataSaveManager
{
    public sealed class DSMRuntimePanel : MonoBehaviour
    {
        [SerializeField] private DSMWidgetConfig? _widgetConfig;
        [SerializeField] private Transform? _container;

        private void Awake() => BuildWidgets();

        public void Rebuild()
        {
            if (_container == null) return;
            foreach (Transform child in _container)
                Destroy(child.gameObject);
            BuildWidgets();
        }

        private void BuildWidgets()
        {
            if (_widgetConfig == null || _container == null)
            {
                Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': _widgetConfig or _container is not assigned in the Inspector.", this);
                return;
            }

            foreach (var entry in DSM.Config.Entries.Where(e => e.Exposed))
            {
                var widgetPrefab = _widgetConfig.GetWidgetPrefab(entry.Type);
                if (widgetPrefab == null) continue;

                var go = Instantiate(widgetPrefab.gameObject, _container);
                var widgetComponent = go.GetComponent<IDSMWidget>();
                if (widgetComponent == null)
                {
                    Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': widget prefab for '{entry.Key}' has no IDSMWidget component.", this);
                    continue;
                }
                widgetComponent.Setup(entry);
            }
        }
    }
}
