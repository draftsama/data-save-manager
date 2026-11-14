#nullable enable

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DataSaveManager
{
    /// <summary>Which methods can be Entry Actions, and how to call one whatever its exact signature.</summary>
    public static class DSMEntryActionSignature
    {
        /// <summary>The Data Type a value of <paramref name="type"/> is stored as, or null when no Data Type fits.</summary>
        public static DSMDataType? DataTypeOf(Type type)
        {
            if (type == typeof(int)) return DSMDataType.Int;
            if (type == typeof(float)) return DSMDataType.Float;
            if (type == typeof(bool)) return DSMDataType.Bool;
            if (type == typeof(string)) return DSMDataType.String;
            if (type == typeof(Vector2)) return DSMDataType.Vector2;
            if (type == typeof(Vector3)) return DSMDataType.Vector3;
            if (type == typeof(Color)) return DSMDataType.Color;
            return null;
        }

        /// <summary>Null when <paramref name="method"/> can be an Entry Action, with <paramref name="dataType"/> the type of Value it produces; otherwise why it can't.</summary>
        public static string? Validate(MethodInfo method, out DSMDataType dataType)
        {
            dataType = default;
            if (method.IsGenericMethodDefinition) return "an Entry Action can't be generic.";

            var returnType = method.ReturnType;
            var valueType = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(UniTask<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;
            var found = DataTypeOf(valueType);
            if (found == null)
                return "an Entry Action returns the new Value as T or UniTask<T>, T being int, float, bool, string, Vector2, Vector3 or Color.";

            var parameters = method.GetParameters();
            var shapeOk = parameters.Length switch
            {
                0 => true,
                1 => parameters[0].ParameterType == typeof(CancellationToken) || parameters[0].ParameterType == valueType,
                2 => parameters[0].ParameterType == valueType && parameters[1].ParameterType == typeof(CancellationToken),
                _ => false
            };
            if (!shapeOk)
                return $"an Entry Action takes nothing, a CancellationToken, the current Value ({valueType.Name}), or the current Value then a CancellationToken.";

            dataType = found.Value;
            return null;
        }

        /// <summary>Calls a method that passed <see cref="Validate"/>, setting its result into Entry <paramref name="key"/>. Exceptions it throws surface unwrapped.</summary>
        internal static Func<DSMStore, CancellationToken, UniTask> Runner(MethodInfo method, object? target, string key, DSMDataType dataType) => dataType switch
        {
            DSMDataType.Int => Runner(key, Typed<int>(method, target)),
            DSMDataType.Float => Runner(key, Typed<float>(method, target)),
            DSMDataType.Bool => Runner(key, Typed<bool>(method, target)),
            DSMDataType.String => Runner(key, Typed<string>(method, target)),
            DSMDataType.Vector2 => Runner(key, Typed<Vector2>(method, target)),
            DSMDataType.Vector3 => Runner(key, Typed<Vector3>(method, target)),
            DSMDataType.Color => Runner(key, Typed<Color>(method, target)),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };

        internal static Func<DSMStore, CancellationToken, UniTask> Runner<T>(string key, Func<T, CancellationToken, UniTask<T>> run) =>
            async (store, ct) =>
            {
                var result = await run(store.Get<T>(key), ct);
                ct.ThrowIfCancellationRequested();
                store.Set(key, result);
            };

        private static Func<T, CancellationToken, UniTask<T>> Typed<T>(MethodInfo method, object? target)
        {
            var parameters = method.GetParameters();
            var returnsTask = method.ReturnType != typeof(T);

            return (current, ct) =>
            {
                object?[]? args = parameters.Length switch
                {
                    0 => null,
                    1 when parameters[0].ParameterType == typeof(CancellationToken) => new object?[] { ct },
                    1 => new object?[] { current },
                    _ => new object?[] { current, ct }
                };

                object? result;
                try
                {
                    result = method.Invoke(target, args);
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    throw;
                }
                return returnsTask ? (UniTask<T>)result! : UniTask.FromResult((T)result!);
            };
        }
    }
}
