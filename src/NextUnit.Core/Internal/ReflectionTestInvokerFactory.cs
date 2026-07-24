using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NextUnit.Internal;

internal static class ReflectionTestInvokerFactory
{
    public static TestMethodWithArgumentsDelegate? Create(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type testClass,
        string methodName,
        Type[] parameterTypes)
    {
        var method = testClass.GetMethod(
            methodName,
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        if (method is null)
        {
            return null;
        }

        var acceptsCancellationToken =
            parameterTypes.Length > 0 &&
            parameterTypes[parameterTypes.Length - 1] == typeof(CancellationToken);

        return async (instance, arguments, cancellationToken) =>
        {
            object?[] actualArguments = arguments;
            if (acceptsCancellationToken && arguments.Length == parameterTypes.Length - 1)
            {
                actualArguments = new object?[arguments.Length + 1];
                arguments.CopyTo(actualArguments, 0);
                actualArguments[arguments.Length] = cancellationToken;
            }

            try
            {
                var result = method.Invoke(method.IsStatic ? null : instance, actualArguments);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                }
                else if (result is ValueTask valueTask)
                {
                    await valueTask.ConfigureAwait(false);
                }
                else if (result is not null && TryGetValueTaskAsTask(result, out task))
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        };
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "MethodInfo.Invoke already materializes the concrete ValueTask<T> return type for boxing. AsTask is a public non-virtual instance method without its own generic parameters on that same closed instantiation.")]
    private static bool TryGetValueTaskAsTask(object result, out Task task)
    {
        var resultType = result.GetType();
        if (!resultType.IsGenericType ||
            resultType.GetGenericTypeDefinition() != typeof(ValueTask<>))
        {
            task = null!;
            return false;
        }

        task = GetValueTaskAsTask(result, resultType);
        return true;
    }

    private static Task GetValueTaskAsTask(
        object result,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        Type resultType)
    {
        return (Task)resultType
            .GetMethod(nameof(ValueTask<int>.AsTask), BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(result, null)!;
    }
}
