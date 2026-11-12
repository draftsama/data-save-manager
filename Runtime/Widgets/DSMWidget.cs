#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DataSaveManager
{
    /// <summary>Base for a Runtime Panel row: labels itself, watches its Entry so every external change (Set/Load/Reset) is reflected, and lets the concrete widget render/commit typed values.</summary>
    public abstract class DSMWidget<T> : MonoBehaviour, IDSMWidget, IDSMLayoutAware
    {
        [SerializeField] private TextMeshProUGUI? _label;

        private DSMStore? _store;

        protected string Key { get; private set; } = string.Empty;

        /// <summary>The store passed to <see cref="Setup(DSMEntryDefinition, DSMStore)"/>, or the global store before Setup.</summary>
        protected DSMStore Store => _store ?? DSM.Store;

        [Obsolete("Pass the store: Setup(entry, store); removed in 3.0.")]
        public void Setup(DSMEntryDefinition entry) => Setup(entry, DSM.Store);

        public void Setup(DSMEntryDefinition entry, DSMStore store)
        {
            _store = store;
            Key = entry.Key;
            if (_label != null) _label.text = entry.DisplayLabel;

            OnSetup(entry);

            store.WatchAsync<T>(Key).ForEachAsync(Show, destroyCancellationToken).Forget();
        }

        /// <summary>Called once from <see cref="Setup"/>, before the Watch subscription starts delivering values.</summary>
        protected virtual void OnSetup(DSMEntryDefinition entry) { }

        public virtual void ApplyLayout(DSMPanelLayout layout)
        {
            var wrap = layout.LabelOverflow == DSMLabelOverflow.Wrap;

            var rowElement = GetOrAddLayoutElement(gameObject);
            rowElement.minHeight = layout.RowHeight;
            rowElement.preferredHeight = wrap ? -1f : layout.RowHeight;

            if (_label != null)
            {
                GetOrAddLayoutElement(_label.gameObject).preferredWidth = layout.LabelWidth;
                _label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                _label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            }

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                text.fontSize = layout.FontSize;
        }

        private static LayoutElement GetOrAddLayoutElement(GameObject go) =>
            go.TryGetComponent<LayoutElement>(out var element) ? element : go.AddComponent<LayoutElement>();

        /// <summary>Writes <paramref name="value"/> into the UI without triggering a Commit round-trip.</summary>
        protected abstract void Show(T value);

        protected void Commit(T value) => Store.Set(Key, value);
    }
}
