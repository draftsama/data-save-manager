#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OperatorConsole
{
    /// <summary>A Command that is live because its owner registered it. Its last outcome lives here, not on any Runtime Panel.</summary>
    public sealed class OCRegisteredCommand
    {
        private readonly Func<CancellationToken, UniTask> _run;
        private readonly CancellationTokenSource _cts = new();

        internal OCRegisteredCommand(
            object owner,
            string title,
            OCCommandOptions options,
            Func<CancellationToken, UniTask> run,
            int sequence,
            int declarationOrder)
        {
            Owner = owner;
            OwnerName = NameOf(owner);
            Title = title;
            Description = string.IsNullOrWhiteSpace(options.Description) ? null : options.Description;
            Confirm = options.Confirm;
            AudienceNames = options.Audiences ?? Array.Empty<string>();
            Header = string.IsNullOrWhiteSpace(options.Header) ? null : options.Header;
            Order = options.Order;
            Sequence = sequence;
            DeclarationOrder = declarationOrder;
            _run = run;
        }

        public object Owner { get; }
        public string OwnerName { get; }
        public string Title { get; }
        public string? Description { get; }
        public bool Confirm { get; }
        public IReadOnlyList<string> AudienceNames { get; }
        public string? Header { get; }
        public int Order { get; }

        public OCCommandStatus Status { get; private set; }

        /// <summary>The message of the exception that made the last run fail; null unless <see cref="Status"/> is Failed.</summary>
        public string? LastError { get; private set; }

        public event Action<OCRegisteredCommand>? StatusChanged;

        internal int Sequence { get; }
        internal int DeclarationOrder { get; }

        public bool IsRunning => Status == OCCommandStatus.Running;

        public void Run() => RunAsync().Forget();

        /// <summary>Runs the Command unless it is already running. Never throws: a failure is recorded in <see cref="Status"/> and logged.</summary>
        public async UniTask RunAsync()
        {
            if (IsRunning || _cts.IsCancellationRequested) return;

            SetStatus(OCCommandStatus.Running, null);
            try
            {
                await _run(_cts.Token);
                SetStatus(OCCommandStatus.Succeeded, null);
            }
            catch (OperationCanceledException)
            {
                SetStatus(OCCommandStatus.NeverRun, null);
            }
            catch (Exception e)
            {
                SetStatus(OCCommandStatus.Failed, e.Message);
                Debug.LogException(e, Owner as Object);
            }
        }

        internal void Cancel() => _cts.Cancel();

        private void SetStatus(OCCommandStatus status, string? error)
        {
            Status = status;
            LastError = error;
            StatusChanged?.Invoke(this);
        }

        internal static string NameOf(object owner) => owner switch
        {
            Component component => component.gameObject.name,
            Object unityObject => unityObject.name,
            Type type => type.Name,
            _ => owner.GetType().Name
        };
    }
}
