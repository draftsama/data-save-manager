#nullable enable

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace OperatorConsole
{
    /// <summary>Which methods can be Commands, and how to call one whatever its exact signature.</summary>
    public static class OCCommandSignature
    {
        /// <summary>Null when <paramref name="method"/> can be a Command; otherwise why it can't.</summary>
        public static string? Validate(MethodInfo method)
        {
            if (method.IsGenericMethodDefinition) return "a Command can't be generic.";

            var parameters = method.GetParameters();
            if (parameters.Length > 1 || (parameters.Length == 1 && parameters[0].ParameterType != typeof(CancellationToken)))
                return "a Command takes no parameters, or a single CancellationToken.";

            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(UniTask))
                return "a Command returns void or UniTask.";

            return null;
        }

        /// <summary>Calls a method that passed <see cref="Validate"/>. Exceptions it throws surface unwrapped.</summary>
        public static Func<object?, CancellationToken, UniTask> Invoker(MethodInfo method)
        {
            var takesToken = method.GetParameters().Length == 1;
            var returnsTask = method.ReturnType == typeof(UniTask);

            return (target, ct) =>
            {
                object? result;
                try
                {
                    result = method.Invoke(target, takesToken ? new object[] { ct } : null);
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    throw;
                }
                return returnsTask ? (UniTask)result! : UniTask.CompletedTask;
            };
        }
    }
}
