#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>A row that watches its Entry so every external change (Set/Load/Reset) is reflected, and lets the concrete widget render/commit typed values.</summary>
    internal abstract class OCWidget<T> : OCWidgetRow
    {
        private OCStore? _store;
        private CancellationTokenSource? _watch;

        protected OCWidget() => RegisterCallback<DetachFromPanelEvent>(_ => StopWatching());

        protected string Key { get; private set; } = string.Empty;

        /// <summary>The store passed to <see cref="Setup"/>, or the global store before Setup.</summary>
        protected OCStore Store => _store ?? OC.Store;

        public override void Setup(OCEntryDefinition entry, OCStore store)
        {
            StopWatching();
            _store = store;
            Key = entry.Key;
            SetLabel(entry.DisplayLabel);

            OnSetup(entry);

            _watch = new CancellationTokenSource();
            store.WatchAsync<T>(Key).ForEachAsync(Show, _watch.Token).Forget();
        }

        /// <summary>Called once from <see cref="Setup"/>, before the Watch subscription starts delivering values.</summary>
        protected virtual void OnSetup(OCEntryDefinition entry) { }

        /// <summary>Stops watching the Entry. Detaching the row from its panel does this too.</summary>
        public void StopWatching()
        {
            if (_watch == null) return;
            _watch.Cancel();
            _watch.Dispose();
            _watch = null;
        }

        /// <summary>Writes <paramref name="value"/> into the UI without triggering a Commit round-trip.</summary>
        protected abstract void Show(T value);

        protected void Commit(T value) => Store.Set(Key, value);
    }
}
