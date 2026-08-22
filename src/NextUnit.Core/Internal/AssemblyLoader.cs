using System.Reflection;
using System.Security;

namespace NextUnit.Internal;

/// <summary>
/// Result of an assembly loading operation.
/// </summary>
internal sealed class AssemblyLoadResult
{
    /// <summary>
    /// Gets the loaded assembly, or null if loading failed.
    /// </summary>
    public Assembly? Assembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly was loaded successfully.
    /// </summary>
    public bool Success => Assembly is not null;

    /// <summary>
    /// Gets the error message if loading failed, or null if successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the error category for logging purposes.
    /// </summary>
    public string? ErrorCategory { get; init; }
}

/// <summary>
/// Utility class for loading assemblies with consistent error handling.
/// </summary>
internal static class AssemblyLoader
{
    /// <summary>
    /// Attempts to load an assembly from the specified path.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <returns>An <see cref="AssemblyLoadResult"/> containing the result of the operation.</returns>
    public static AssemblyLoadResult TryLoadAssembly(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            // Do not include the assembly path here because the caller already logs it.
            return new AssemblyLoadResult
            {
                ErrorMessage = "Assembly file not found",
                ErrorCategory = "file not found"
            };
        }

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            return new AssemblyLoadResult { Assembly = assembly };
        }
        catch (FileNotFoundException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "file not found"
            };
        }
        catch (BadImageFormatException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "bad image format"
            };
        }
        catch (FileLoadException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "file load error"
            };
        }
        catch (IOException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "I/O error"
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "access denied"
            };
        }
        catch (SecurityException ex)
        {
            return new AssemblyLoadResult
            {
                ErrorMessage = ex.Message,
                ErrorCategory = "security error"
            };
        }
    }

    /// <summary>
    /// Gets the GeneratedTestRegistry type from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to search.</param>
    /// <returns>The registry type if found; otherwise, null.</returns>
    public static Type? GetTestRegistryType(Assembly assembly)
    {
        return assembly.GetType("NextUnit.Generated.GeneratedTestRegistry");
    }

    /// <summary>
    /// Gets the value of a static property from a type.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="type">The type containing the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value if found and of the correct type; otherwise, default.</returns>
    public static T? GetStaticPropertyValue<T>(Type type, string propertyName) where T : class
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        return property?.GetValue(null) as T;
    }

    /// <summary>
    /// Gets the value of a static property of value type from a type.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="type">The type containing the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value, or null when the property is absent or of another type.</returns>
    /// <remarks>
    /// Separate from <see cref="GetStaticPropertyValue{T}"/> rather than a relaxed constraint on it:
    /// that method returns <c>default</c> for a miss, and <c>default(int)</c> is <c>0</c>, which a
    /// caller reading a limit cannot tell from a registry that really reports zero. The nullable
    /// return keeps "no such property" distinguishable, which is what lets a registry generated
    /// before the property existed fall back rather than be refused.
    /// </remarks>
    public static T? GetStaticStructPropertyValue<T>(Type type, string propertyName) where T : struct
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        return property?.GetValue(null) as T?;
    }
}
