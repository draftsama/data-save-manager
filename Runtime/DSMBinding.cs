#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>No-code, one-way binding: pushes an Entry's Value into a member of a component on the same GameObject, live.</summary>
    [AddComponentMenu("DSM/DSM Binding")]
    public sealed class DSMBinding : MonoBehaviour
    {
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private Component? _target;
        [SerializeField] private string _member = string.Empty;
        [SerializeField] private string _format = "{0}";

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _linkedCts;

        public string Key => _key;
        public Component? Target => _target;
        public string Member => _member;
        public string Format => _format;

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _linkedCts?.Dispose();
            _linkedCts = null;
        }

        /// <summary>Re-resolves the Key/Target/Member and restarts the watch. Call after changing the serialized fields at runtime.</summary>
        public void Rebind()
        {
            OnDisable();
            Bind();
        }

        private void Bind()
        {
            if (!DSM.Config.TryGetEntry(_key, out var entry))
            {
                Debug.LogError($"DSM Binding: no entry definition for key '{_key}'.", this);
                return;
            }

            if (_target == null)
            {
                Debug.LogError("DSM Binding: no target component assigned.", this);
                return;
            }

            if (_target.gameObject != gameObject)
            {
                Debug.LogError("DSM Binding: target must be a component on the same GameObject.", this);
                return;
            }

            var valueType = DSMEntryDefinition.ClrTypeOf(entry.Type);

            _cts = new CancellationTokenSource();
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, destroyCancellationToken);
            var token = _linkedCts.Token;

            switch (entry.Type)
            {
                case DSMDataType.Int: Bind<int>(valueType, token); break;
                case DSMDataType.Float: Bind<float>(valueType, token); break;
                case DSMDataType.Bool: Bind<bool>(valueType, token); break;
                case DSMDataType.String: Bind<string>(valueType, token); break;
                case DSMDataType.Vector2: Bind<Vector2>(valueType, token); break;
                case DSMDataType.Vector3: Bind<Vector3>(valueType, token); break;
                case DSMDataType.Color: Bind<Color>(valueType, token); break;
                default:
                    Debug.LogError($"DSM Binding: unsupported entry type '{entry.Type}'.", this);
                    break;
            }
        }

        private void Bind<T>(Type valueType, CancellationToken token)
        {
            Action<T> rawSetter;
            if (DSMBindingMembers.TryCreateSetter<T>(_target!, _member, out var typedSetter))
            {
                rawSetter = typedSetter;
            }
            else if (typeof(T) != typeof(string) &&
                     DSMBindingMembers.TryCreateSetter<string>(_target!, _member, out var stringSetter))
            {
                // Formatted fallback: the member is a string setter, so format the value through _format first.
                // loggedFormatError keeps a bad format string from spamming the console on every value change.
                var loggedFormatError = false;
                rawSetter = value =>
                {
                    if (!DSMBindingMembers.TryFormat(_format, value, out var text, out var error))
                    {
                        if (!loggedFormatError)
                        {
                            Debug.LogError($"DSM Binding: format '{_format}' failed on '{_target!.GetType().Name}': {error}", this);
                            loggedFormatError = true;
                        }
                        text = value?.ToString() ?? "";
                    }
                    stringSetter(text);
                };
            }
            else
            {
                Debug.LogError(
                    $"DSM Binding: member '{_member}' not found on '{_target!.GetType().Name}', or its type doesn't match '{valueType.Name}' or 'string'.",
                    this);
                return;
            }

            var target = _target;
            void GuardedSetter(T value)
            {
                if (target == null)
                {
                    _cts?.Cancel();
                    return;
                }
                try { rawSetter(value); }
                catch (Exception e) when (e is MissingReferenceException or NullReferenceException)
                {
                    _cts?.Cancel();
                }
            }

            DSM.WatchAsync<T>(_key).ForEachAsync(GuardedSetter, token).Forget();
        }
    }
}
