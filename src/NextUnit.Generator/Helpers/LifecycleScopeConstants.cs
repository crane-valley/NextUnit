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
