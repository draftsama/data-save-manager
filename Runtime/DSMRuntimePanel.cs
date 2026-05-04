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

    private void Start() => BuildWidgets();

    public void Rebuild()
    {
        foreach (Transform child in _container!)
            Destroy(child.gameObject);
        BuildWidgets();
    }

    private void BuildWidgets()
    {
        if (_config == null || _widgetConfig == null || _container == null) return;
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
            go.GetComponent<IDSMWidget>()?.Setup(entry.Key, entry.Type, label, slot);
        }
    }
}
