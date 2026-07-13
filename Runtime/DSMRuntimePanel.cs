#nullable enable

using System;
using System.Linq;
using UnityEngine;

public sealed class DSMRuntimePanel : MonoBehaviour
{
    [SerializeField] private DSMConfig? _config;
    [SerializeField] private DSMWidgetConfig? _widgetConfig;
    [SerializeField] private Transform? _container;

    [SerializeField] private string _slotName = "default";

    private void Awake() => BuildWidgets();

    public void Rebuild()
    {
        foreach (Transform child in _container!)
            Destroy(child.gameObject);
        BuildWidgets();
    }

    private void BuildWidgets()
    {
        if (_config == null || _widgetConfig == null || _container == null)
        {
            Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': _config, _widgetConfig, or _container is not assigned in the Inspector.", this);
            return;
        }
        if (!DSM.GetAllSlots().Contains(_slotName))
            throw new InvalidOperationException($"DSMRuntimePanel: slot '{_slotName}' not found");

        var slot = DSM.GetSlot(_slotName);
        slot.Load();

        foreach (var entry in _config.ExposedEntries)
        {
            var widgetPrefab = _widgetConfig.GetWidgetPrefab(entry.Type);
            if (widgetPrefab == null) continue;

            var go = Instantiate(widgetPrefab.gameObject, _container);
            var label = string.IsNullOrEmpty(entry.Label) ? entry.Key : entry.Label;

            var widgetComponent = go.GetComponent<IDSMWidget>();
            if (widgetComponent == null)
            {
                Debug.LogError($"DSMRuntimePanel on '{gameObject.name}': widget prefab for '{entry.Key}' has no IDSMWidget component.", this);
                continue;
            }
            widgetComponent.Setup(entry.Key, entry.Type, label, slot);
        }
    }
}
