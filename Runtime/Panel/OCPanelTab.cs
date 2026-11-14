#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    /// <summary>One tab of the Runtime Panel's tab bar; an underline marks the selected tab.</summary>
    public sealed class OCPanelTab : MonoBehaviour, IOCLayoutAware
    {
        [SerializeField] private Button? _button;
        [SerializeField] private TMP_Text? _label;
        [SerializeField] private GameObject? _underline;
        [SerializeField] private Color _selectedColor = new(0.91f, 0.92f, 0.94f, 1f);
        [SerializeField] private Color _unselectedColor = new(0.6f, 0.63f, 0.69f, 1f);

        public Button? Button => _button;

        public void SetSelected(bool selected)
        {
            if (_underline != null) _underline.SetActive(selected);
            if (_label != null) _label.color = selected ? _selectedColor : _unselectedColor;
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            var element = TryGetComponent<LayoutElement>(out var e) ? e : gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = layout.TabHeight;
            element.minHeight = layout.TabHeight;
            if (_label != null) _label.fontSize = layout.FontSize;
        }
    }
}
