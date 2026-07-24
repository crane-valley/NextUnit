namespace NextUnit.Internal;

/// <summary>
/// Converts runtime data-source values using only the implicit numeric conversions
/// that a statically compiled test invocation would permit.
/// </summary>
public static class ArgumentConverter
{
    /// <summary>
    /// Converts a runtime data-source value to a test method parameter type.
    /// </summary>
    /// <typeparam name="T">The compile-time parameter type.</typeparam>
    /// <param name="value">The value supplied by the runtime data source.</param>
    /// <param name="parameterName">The test method parameter name.</param>
    /// <param name="methodName">The test method name.</param>
    /// <returns>The value converted to <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the value cannot be passed to the parameter using an implicit conversion.
    /// </exception>
    public static T Convert<T>(object? value, string parameterName, string methodName)
    {
        if (value is T typed)
        {
            return typed;
        }

        if (value is null)
        {
            if (default(T) is null)
            {
                return default!;
            }

            throw CreateException<T>(value, parameterName, methodName);
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (TryConvertNumeric(value, targetType, out var converted))
        {
            return (T)converted;
        }

        throw CreateException<T>(value, parameterName, methodName);
    }

    private static bool TryConvertNumeric(object value, Type targetType, out object converted)
    {
        if (targetType == typeof(short))
        {
            converted = value switch
            {
                sbyte number => (short)number,
                byte number => (short)number,
                _ => null!
            };
        }
        else if (targetType == typeof(ushort))
        {
            converted = value switch
            {
                byte number => (ushort)number,
                char number => (ushort)number,
                _ => null!
            };
        }
        else if (targetType == typeof(int))
        {
            converted = value switch
            {
                sbyte number => (int)number,
                byte number => (int)number,
                short number => (int)number,
                ushort number => (int)number,
                char number => (int)number,
                _ => null!
            };
        }
        else if (targetType == typeof(uint))
        {
            converted = value switch
            {
                byte number => (uint)number,
                ushort number => (uint)number,
                char number => (uint)number,
                _ => null!
            };
        }
        else if (targetType == typeof(long))
        {
            converted = value switch
            {
                sbyte number => (long)number,
                byte number => (long)number,
                short number => (long)number,
                ushort number => (long)number,
                int number => (long)number,
                uint number => (long)number,
                char number => (long)number,
                _ => null!
            };
        }
        else if (targetType == typeof(ulong))
        {
            converted = value switch
            {
                byte number => (ulong)number,
                ushort number => (ulong)number,
                uint number => (ulong)number,
                char number => (ulong)number,
                _ => null!
            };
        }
        else if (targetType == typeof(float))
        {
            converted = value switch
            {
                sbyte number => (float)number,
                byte number => (float)number,
                short number => (float)number,
                ushort number => (float)number,
                int number => (float)number,
                uint number => (float)number,
                long number => (float)number,
                ulong number => (float)number,
                char number => (float)number,
                _ => null!
            };
        }
        else if (targetType == typeof(double))
        {
            converted = value switch
            {
                sbyte number => (double)number,
                byte number => (double)number,
                short number => (double)number,
                ushort number => (double)number,
                int number => (double)number,
                uint number => (double)number,
                long number => (double)number,
                ulong number => (double)number,
                char number => (double)number,
                float number => (double)number,
                _ => null!
            };
        }
        else if (targetType == typeof(decimal))
        {
            converted = value switch
            {
                sbyte number => (decimal)number,
                byte number => (decimal)number,
                short number => (decimal)number,
                ushort number => (decimal)number,
                int number => (decimal)number,
                uint number => (decimal)number,
                long number => (decimal)number,
                ulong number => (decimal)number,
                char number => (decimal)number,
                _ => null!
            };
        }
        else
        {
            converted = null!;
        }

        return converted is not null;
    }

    private static ArgumentException CreateException<T>(
        object? value,
        string parameterName,
        string methodName)
    {
        var actualType = value?.GetType().ToString() ?? "<null>";
        return new ArgumentException(
            $"Value for parameter '{parameterName}' in test method '{methodName}' has runtime type '{actualType}' and cannot be implicitly converted to '{typeof(T)}'.",
            parameterName);
    }
}
