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
                if (method.Invoke(method.IsStatic ? null : instance, actualArguments) is Task task)
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
}
