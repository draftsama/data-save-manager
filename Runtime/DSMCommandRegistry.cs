#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DataSaveManager
{
    /// <summary>The Registered Commands of this app. Owners register while they are live and unregister when they go away.</summary>
    public sealed class DSMCommandRegistry
    {
        private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Func<DSMConfig?> _config;
        private readonly List<DSMRegisteredCommand> _commands = new();
        private readonly List<object> _owners = new();
        private int _nextSequence;

        public DSMCommandRegistry(Func<DSMConfig?> config) => _config = config;

        /// <summary>Every Registered Command, by Order, then registration, then declaration.</summary>
        public IReadOnlyList<DSMRegisteredCommand> Commands => _commands;

        /// <summary>Raised after Commands are added or removed.</summary>
        public event Action? Changed;

        /// <summary>Registers the <see cref="DSMCommandAttribute"/> instance methods of <paramref name="owner"/>, inherited ones included. Registering an owner twice does nothing.</summary>
        public void Register(object owner)
        {
            if (owner is Type type)
            {
                Register(type);
                return;
            }
            if (!TryAddOwner(owner)) return;

            var sequence = _nextSequence++;
            var seen = new HashSet<MethodInfo>();
            for (var t = owner.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
            {
                foreach (var method in t.GetMethods(InstanceMethods))
                {
                    if (!seen.Add(method.GetBaseDefinition())) continue;
                    AddFromAttribute(owner, owner, method, sequence);
                }
            }
            Sort();
            Changed?.Invoke();
        }

        /// <summary>Registers the <see cref="DSMCommandAttribute"/> static methods declared on <paramref name="type"/>.</summary>
        public void Register(Type type)
        {
            if (!TryAddOwner(type)) return;

            var sequence = _nextSequence++;
            foreach (var method in type.GetMethods(StaticMethods))
                AddFromAttribute(type, null, method, sequence);
            Sort();
            Changed?.Invoke();
        }

        /// <summary>Registers one Command from code. Unregistering <paramref name="owner"/> removes it.</summary>
        public DSMRegisteredCommand Register(object owner, string title, Func<CancellationToken, UniTask> run, DSMCommandOptions? options = null)
        {
            if (!ContainsOwner(owner)) _owners.Add(owner);

            options ??= new DSMCommandOptions();
            var command = new DSMRegisteredCommand(owner, title, options, run, _nextSequence++, 0);
            WarnUnknownAudiences(command);
            _commands.Add(command);
            Sort();
            Changed?.Invoke();
            return command;
        }

        /// <summary>Stops and removes every Command <paramref name="owner"/> registered.</summary>
        public void Unregister(object owner)
        {
            var index = IndexOfOwner(owner);
            if (index < 0) return;
            _owners.RemoveAt(index);

            for (var i = _commands.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_commands[i].Owner, owner)) continue;
                var command = _commands[i];
                _commands.RemoveAt(i);
                command.Cancel();
            }
            Changed?.Invoke();
        }

        /// <summary>Stops and removes every Command.</summary>
        public void Clear()
        {
            foreach (var command in _commands) command.Cancel();
            _commands.Clear();
            _owners.Clear();
            Changed?.Invoke();
        }

        /// <summary>The Title, with the owner's name added when another Registered Command has the same Title.</summary>
        public string DisplayTitle(DSMRegisteredCommand command)
        {
            foreach (var other in _commands)
            {
                if (!ReferenceEquals(other, command) && string.Equals(other.Title, command.Title, StringComparison.Ordinal))
                    return $"{command.Title} ({command.OwnerName})";
            }
            return command.Title;
        }

        private void AddFromAttribute(object owner, object? target, MethodInfo method, int sequence)
        {
            var attribute = method.GetCustomAttribute<DSMCommandAttribute>(true);
            if (attribute == null) return;

            var error = DSMCommandSignature.Validate(method);
            if (error != null)
            {
                Debug.LogError($"DSM: [DSMCommand] {method.DeclaringType?.Name}.{method.Name} is skipped: {error}", owner as Object);
                return;
            }

            var invoke = DSMCommandSignature.Invoker(method);
            var options = new DSMCommandOptions
            {
                Description = attribute.Description,
                Confirm = attribute.Confirm,
                Audiences = attribute.Audiences,
                Header = attribute.Header,
                Order = attribute.Order
            };
            var command = new DSMRegisteredCommand(owner, attribute.Title, options, ct => invoke(target, ct), sequence, method.MetadataToken);
            WarnUnknownAudiences(command);
            _commands.Add(command);
        }

        private void WarnUnknownAudiences(DSMRegisteredCommand command)
        {
            var config = _config();
            if (config == null) return;
            foreach (var name in command.AudienceNames)
            {
                if (!config.TryGetAudienceByName(name, out _))
                    Debug.LogWarning($"DSM: Command '{command.Title}' names Audience '{name}', which the Config doesn't declare.", command.Owner as Object);
            }
        }

        private void Sort() => _commands.Sort((a, b) =>
        {
            var byOrder = a.Order.CompareTo(b.Order);
            if (byOrder != 0) return byOrder;
            var bySequence = a.Sequence.CompareTo(b.Sequence);
            return bySequence != 0 ? bySequence : a.DeclarationOrder.CompareTo(b.DeclarationOrder);
        });

        private bool TryAddOwner(object owner)
        {
            if (ContainsOwner(owner)) return false;
            _owners.Add(owner);
            return true;
        }

        private bool ContainsOwner(object owner) => IndexOfOwner(owner) >= 0;

        private int IndexOfOwner(object owner)
        {
            for (var i = 0; i < _owners.Count; i++)
                if (ReferenceEquals(_owners[i], owner)) return i;
            return -1;
        }
    }
}
