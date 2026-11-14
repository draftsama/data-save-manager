#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DataSaveManager
{
    /// <summary>An Entry Action that is live because its owner registered it. Its last outcome lives here, not on any Runtime Panel.</summary>
    public sealed class DSMRegisteredEntryAction
    {
        private readonly Func<DSMStore, CancellationToken, UniTask> _run;
        private readonly CancellationTokenSource _cts = new();

        internal DSMRegisteredEntryAction(
            object owner,
            string key,
            DSMDataType dataType,
            string title,
            DSMEntryActionOptions options,
            Func<DSMStore, CancellationToken, UniTask> run,
            int sequence,
            int declarationOrder)
        {
            Owner = owner;
            OwnerName = DSMRegisteredCommand.NameOf(owner);
            Key = key;
            DataType = dataType;
            Title = title;
            Confirm = options.Confirm;
            Order = options.Order;
            Sequence = sequence;
            DeclarationOrder = declarationOrder;
            _run = run;
        }

        public object Owner { get; }
        public string OwnerName { get; }
        public string Key { get; }
        public DSMDataType DataType { get; }
        public string Title { get; }
        public bool Confirm { get; }
        public int Order { get; }

        public DSMCommandStatus Status { get; private set; }

        /// <summary>The message of the exception that made the last run fail; null unless <see cref="Status"/> is Failed.</summary>
        public string? LastError { get; private set; }

        public event Action<DSMRegisteredEntryAction>? StatusChanged;

        internal int Sequence { get; }
        internal int DeclarationOrder { get; }

        public bool IsRunning => Status == DSMCommandStatus.Running;

        public void Run(DSMStore store) => RunAsync(store).Forget();

        /// <summary>Runs the Entry Action unless it is already running, then sets its result into <see cref="Key"/> in <paramref name="store"/>. Never throws: a failure is recorded in <see cref="Status"/> and logged.</summary>
        public async UniTask RunAsync(DSMStore store)
        {
            if (IsRunning || _cts.IsCancellationRequested) return;

            SetStatus(DSMCommandStatus.Running, null);
            try
            {
                await _run(store, _cts.Token);
                SetStatus(DSMCommandStatus.Succeeded, null);
            }
            catch (OperationCanceledException)
            {
                SetStatus(DSMCommandStatus.NeverRun, null);
            }
            catch (Exception e)
            {
                SetStatus(DSMCommandStatus.Failed, e.Message);
                Debug.LogException(e, Owner as Object);
            }
        }

        internal void Cancel() => _cts.Cancel();

        private void SetStatus(DSMCommandStatus status, string? error)
        {
            Status = status;
            LastError = error;
            StatusChanged?.Invoke(this);
        }
    }
}
