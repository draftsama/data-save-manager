#nullable enable

using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using TMPro;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>Base for a Runtime Panel row: labels itself, watches its Entry so every external change (Set/Load/Reset) is reflected, and lets the concrete widget render/commit typed values.</summary>
    public abstract class DSMWidget<T> : MonoBehaviour, IDSMWidget
    {
        [SerializeField] private TextMeshProUGUI? _label;

        protected string Key { get; private set; } = string.Empty;

        public void Setup(DSMEntryDefinition entry)
        {
            Key = entry.Key;
            if (_label != null) _label.text = entry.DisplayLabel;

            DSM.WatchAsync<T>(Key).ForEachAsync(Show, destroyCancellationToken).Forget();
        }

        /// <summary>Writes <paramref name="value"/> into the UI without triggering a Commit round-trip.</summary>
        protected abstract void Show(T value);

        protected void Commit(T value) => DSM.Set(Key, value);
    }
}
