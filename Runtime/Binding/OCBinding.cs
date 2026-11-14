#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>No-code, one-way binding: pushes an Entry's Value into one or more members of components on the same GameObject, live.</summary>
    [AddComponentMenu("Operator Console/Binding")]
    public sealed class OCBinding : MonoBehaviour, ISerializationCallbackReceiver
    {
        /// <summary>One member to drive: a component on the same GameObject, one of its members, and the format used if that member is a string.</summary>
        [Serializable]
        public sealed class Link
        {
            [SerializeField] private Component? _target;
            [SerializeField] private string _member = string.Empty;
            [SerializeField] private string _format = "{0}";

            public Link() { }

            internal Link(Component? target, string member, string format)
            {
                _target = target;
                _member = member;
                _format = format;
            }

            public Component? Target => _target;
            public string Member => _member;
            public string Format => _format;
        }

        [SerializeField] private string _key = string.Empty;
        [SerializeField] private List<Link> _links = new();

        // Pre-list single-member fields, kept only so existing scenes/prefabs migrate in OnAfterDeserialize.
        [SerializeField, HideInInspector] private Component? _target;
        [SerializeField, HideInInspector] private string _member = string.Empty;
        [SerializeField, HideInInspector] private string _format = "{0}";

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _linkedCts;

        public string Key => _key;
        public IReadOnlyList<Link> Links => _links;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(_member)) return;
            if (_links.Count == 0) _links.Add(new Link(_target, _member, _format));
            _target = null;
            _member = string.Empty;
            _format = "{0}";
        }

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _linkedCts?.Dispose();
            _linkedCts = null;
        }

        /// <summary>Re-resolves the Key and Links and restarts the watch. Call after changing the serialized fields at runtime.</summary>
        public void Rebind()
        {
            OnDisable();
            Bind();
        }

        private void Bind()
        {
            if (!OC.Config.TryGetEntry(_key, out var entry))
            {
                Debug.LogError($"OC Binding: no entry definition for key '{_key}'.", this);
                return;
            }

            if (_links.Count == 0)
            {
                Debug.LogError("OC Binding: no links configured.", this);
                return;
            }

            var valueType = OCEntryDefinition.ClrTypeOf(entry.Type);

            _cts = new CancellationTokenSource();
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, destroyCancellationToken);
            var token = _linkedCts.Token;

            switch (entry.Type)
            {
                case OCDataType.Int: Bind<int>(valueType, token); break;
                case OCDataType.Float: Bind<float>(valueType, token); break;
                case OCDataType.Bool: Bind<bool>(valueType, token); break;
                case OCDataType.String: Bind<string>(valueType, token); break;
                case OCDataType.Vector2: Bind<Vector2>(valueType, token); break;
                case OCDataType.Vector3: Bind<Vector3>(valueType, token); break;
                case OCDataType.Color: Bind<Color>(valueType, token); break;
                default:
                    Debug.LogError($"OC Binding: unsupported entry type '{entry.Type}'.", this);
                    break;
            }
        }

        private void Bind<T>(Type valueType, CancellationToken token)
        {
            var setters = new List<(Component target, Action<T> set)>(_links.Count);
            foreach (var link in _links)
            {
                if (TryCreateSetter<T>(link, valueType, out var setter))
                    setters.Add((link.Target!, setter));
            }

            if (setters.Count == 0) return;

            void Apply(T value)
            {
                for (var i = setters.Count - 1; i >= 0; i--)
                {
                    var (target, set) = setters[i];
                    if (target == null)
                    {
                        setters.RemoveAt(i);
                        continue;
                    }
                    try { set(value); }
                    catch (Exception e) when (e is MissingReferenceException or NullReferenceException)
                    {
                        setters.RemoveAt(i);
                    }
                }

                if (setters.Count == 0) _cts?.Cancel();
            }

            OC.WatchAsync<T>(_key).ForEachAsync(Apply, token).Forget();
        }

        private bool TryCreateSetter<T>(Link link, Type valueType, out Action<T> setter)
        {
            setter = null!;
            var target = link.Target;
            if (target == null)
            {
                Debug.LogError("OC Binding: a link has no target component assigned.", this);
                return false;
            }

            if (target.gameObject != gameObject)
            {
                Debug.LogError("OC Binding: target must be a component on the same GameObject.", this);
                return false;
            }

            var format = link.Format;
            if (OCBindingMembers.TryCreateSetter<T>(target, link.Member, format,
                    error => Debug.LogError($"OC Binding: format '{format}' failed on '{target.GetType().Name}': {error}", this),
                    out setter))
                return true;

            Debug.LogError(
                $"OC Binding: member '{link.Member}' not found on '{target.GetType().Name}', or its type doesn't match '{valueType.Name}' or 'string'.",
                this);
            return false;
        }
    }
}
