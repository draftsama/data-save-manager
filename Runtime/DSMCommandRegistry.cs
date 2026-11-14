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
    /// <summary>The Registered Commands and Entry Actions of this app. Owners register while they are live and unregister when they go away.</summary>
    public sealed class DSMCommandRegistry
    {
        private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Func<DSMConfig?> _config;
        private readonly List<DSMRegisteredCommand> _commands = new();
        private readonly List<DSMRegisteredEntryAction> _entryActions = new();
        private readonly List<object> _owners = new();
        private int _nextSequence;

        public DSMCommandRegistry(Func<DSMConfig?> config) => _config = config;

        /// <summary>Every Registered Command, by Order, then registration, then declaration.</summary>
        public IReadOnlyList<DSMRegisteredCommand> Commands => _commands;

        /// <summary>Every registered Entry Action, by Order, then registration, then declaration.</summary>
        public IReadOnlyList<DSMRegisteredEntryAction> EntryActions => _entryActions;

        /// <summary>The registered Entry Actions of Entry <paramref name="key"/>, in <see cref="EntryActions"/> order.</summary>
        public List<DSMRegisteredEntryAction> EntryActionsFor(string key)
        {
            var result = new List<DSMRegisteredEntryAction>();
            foreach (var action in _entryActions)
                if (string.Equals(action.Key, key, StringComparison.Ordinal)) result.Add(action);
            return result;
        }

        /// <summary>Raised after Commands or Entry Actions are added or removed.</summary>
        public event Action? Changed;

        /// <summary>Registers the <see cref="DSMCommandAttribute"/> and <see cref="DSMEntryActionAttribute"/> instance methods of <paramref name="owner"/>, inherited ones included. Registering an owner twice does nothing.</summary>
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

        /// <summary>Registers the <see cref="DSMCommandAttribute"/> and <see cref="DSMEntryActionAttribute"/> static methods declared on <paramref name="type"/>.</summary>
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

        /// <summary>
        /// Registers one Entry Action from code: a button beside Entry <paramref name="key"/> whose result becomes its Value.
        /// <typeparamref name="T"/> must match the Entry's Data Type. Throw <see cref="OperationCanceledException"/> from <paramref name="run"/> to leave the Value unchanged.
        /// Unregistering <paramref name="owner"/> removes it. Returns null when <typeparamref name="T"/> fits no Data Type or not the Entry's.
        /// </summary>
        public DSMRegisteredEntryAction? Register<T>(object owner, string key, string title, Func<T, CancellationToken, UniTask<T>> run, DSMEntryActionOptions? options = null)
        {
            var dataType = DSMEntryActionSignature.DataTypeOf(typeof(T));
            if (dataType == null)
            {
                Debug.LogError($"DSM: Entry Action '{title}' on '{key}' is skipped: {typeof(T).Name} is not a Data Type.", owner as Object);
                return null;
            }
            if (!CheckEntry(key, title, dataType.Value, owner)) return null;

            if (!ContainsOwner(owner)) _owners.Add(owner);
            var action = new DSMRegisteredEntryAction(owner, key, dataType.Value, title, options ?? new DSMEntryActionOptions(),
                DSMEntryActionSignature.Runner(key, run), _nextSequence++, 0);
            _entryActions.Add(action);
            Sort();
            Changed?.Invoke();
            return action;
        }

        /// <summary>Stops and removes every Command and Entry Action <paramref name="owner"/> registered.</summary>
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
            for (var i = _entryActions.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_entryActions[i].Owner, owner)) continue;
                var action = _entryActions[i];
                _entryActions.RemoveAt(i);
                action.Cancel();
            }
            Changed?.Invoke();
        }

        /// <summary>Stops and removes every Command and Entry Action.</summary>
        public void Clear()
        {
            foreach (var command in _commands) command.Cancel();
            foreach (var action in _entryActions) action.Cancel();
            _commands.Clear();
            _entryActions.Clear();
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

        /// <summary>The Title, with the owner's name added when another Entry Action of the same Entry has the same Title.</summary>
        public string DisplayTitle(DSMRegisteredEntryAction action)
        {
            foreach (var other in _entryActions)
            {
                if (!ReferenceEquals(other, action)
                    && string.Equals(other.Key, action.Key, StringComparison.Ordinal)
                    && string.Equals(other.Title, action.Title, StringComparison.Ordinal))
                    return $"{action.Title} ({action.OwnerName})";
            }
            return action.Title;
        }

        private void AddFromAttribute(object owner, object? target, MethodInfo method, int sequence)
        {
            AddCommandFromAttribute(owner, target, method, sequence);
            AddEntryActionFromAttribute(owner, target, method, sequence);
        }

        private void AddEntryActionFromAttribute(object owner, object? target, MethodInfo method, int sequence)
        {
            var attribute = method.GetCustomAttribute<DSMEntryActionAttribute>(true);
            if (attribute == null) return;

            var error = DSMEntryActionSignature.Validate(method, out var dataType);
            if (error != null)
            {
                Debug.LogError($"DSM: [DSMEntryAction] {method.DeclaringType?.Name}.{method.Name} is skipped: {error}", owner as Object);
                return;
            }
            if (!CheckEntry(attribute.Key, attribute.Title, dataType, owner)) return;

            var options = new DSMEntryActionOptions { Confirm = attribute.Confirm, Order = attribute.Order };
            var run = DSMEntryActionSignature.Runner(method, target, attribute.Key, dataType);
            _entryActions.Add(new DSMRegisteredEntryAction(owner, attribute.Key, dataType, attribute.Title, options, run, sequence, method.MetadataToken));
        }

        // A missing or hidden Entry only warns: the Config can gain the Entry later, and the action shows once it does.
        private bool CheckEntry(string key, string title, DSMDataType dataType, object owner)
        {
            var config = _config();
            if (config == null) return true;

            if (!config.TryGetEntry(key, out var entry))
            {
                Debug.LogWarning($"DSM: Entry Action '{title}' names Entry '{key}', which the Config doesn't define.", owner as Object);
                return true;
            }
            if (entry.Type != dataType)
            {
                Debug.LogError($"DSM: Entry Action '{title}' on '{key}' is skipped: it produces {dataType}, but the Entry is {entry.Type}.", owner as Object);
                return false;
            }
            if (!entry.Exposed)
                Debug.LogWarning($"DSM: Entry Action '{title}' is on '{key}', which is not an Exposed Entry, so no Runtime Panel shows it.", owner as Object);
            return true;
        }

        private void AddCommandFromAttribute(object owner, object? target, MethodInfo method, int sequence)
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

        private void Sort()
        {
            _commands.Sort((a, b) => Compare(a.Order, a.Sequence, a.DeclarationOrder, b.Order, b.Sequence, b.DeclarationOrder));
            _entryActions.Sort((a, b) => Compare(a.Order, a.Sequence, a.DeclarationOrder, b.Order, b.Sequence, b.DeclarationOrder));
        }

        private static int Compare(int orderA, int sequenceA, int declarationA, int orderB, int sequenceB, int declarationB)
        {
            var byOrder = orderA.CompareTo(orderB);
            if (byOrder != 0) return byOrder;
            var bySequence = sequenceA.CompareTo(sequenceB);
            return bySequence != 0 ? bySequence : declarationA.CompareTo(declarationB);
        }

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
