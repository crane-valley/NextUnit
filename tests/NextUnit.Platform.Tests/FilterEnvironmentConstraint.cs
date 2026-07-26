namespace NextUnit.Platform.Tests;

/// <summary>
/// Names the <c>[NotInParallel]</c> constraint shared by every test that constructs a
/// <see cref="NextUnitFramework"/> or mutates a filter environment variable.
/// </summary>
/// <remarks>
/// The framework constructor loads its filter configuration from process-wide environment variables,
/// so a test that installs a deliberately invalid value and a test that expects the ambient value
/// cannot run at the same time. Sharing one constraint key serializes exactly those tests.
/// </remarks>
internal static class FilterEnvironmentConstraint
{
    public const string Key = "NextUnit.FilterEnvironment";
}
