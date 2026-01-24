namespace NextUnit.Generator.Helpers;

/// <summary>
/// Constants for lifecycle scope values.
/// These must match the values in NextUnit.Core.LifecycleScope enum.
/// </summary>
internal static class LifecycleScopeConstants
{
    /// <summary>
    /// Executes before or after each individual test.
    /// </summary>
    public const int Test = 0;

    /// <summary>
    /// Executes before or after all tests in a class.
    /// </summary>
    public const int Class = 1;

    /// <summary>
    /// Executes before or after all tests in an assembly.
    /// </summary>
    public const int Assembly = 2;

    /// <summary>
    /// Executes before or after the entire test session.
    /// </summary>
    public const int Session = 3;
}

/// <summary>
/// Constants for shared type values.
/// These must match the values in NextUnit.Core.SharedType enum.
/// </summary>
internal static class SharedTypeConstants
{
    /// <summary>
    /// New instance per test (no sharing).
    /// </summary>
    public const int None = 0;

    /// <summary>
    /// Shared by specified key.
    /// </summary>
    public const int Keyed = 1;

    /// <summary>
    /// Shared within test class.
    /// </summary>
    public const int PerClass = 2;

    /// <summary>
    /// Shared within assembly.
    /// </summary>
    public const int PerAssembly = 3;

    /// <summary>
    /// Shared across session.
    /// </summary>
    public const int PerSession = 4;
}
