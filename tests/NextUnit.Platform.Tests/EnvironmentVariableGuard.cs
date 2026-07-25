namespace NextUnit.Platform.Tests;

/// <summary>
/// Sets an environment variable and restores its previous value on disposal.
/// </summary>
/// <remarks>
/// Environment variables are process-wide, so a test that leaves one set would leak filter
/// configuration into every test that runs after it. Tying the restore to <c>using</c> means the
/// restore cannot be skipped by an early return or an exception.
/// </remarks>
internal sealed class EnvironmentVariableGuard : IDisposable
{
    private readonly string _name;
    private readonly string? _originalValue;

    private EnvironmentVariableGuard(string name, string? originalValue)
    {
        _name = name;
        _originalValue = originalValue;
    }

    public static EnvironmentVariableGuard Set(string name, string? value)
    {
        var guard = new EnvironmentVariableGuard(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
        return guard;
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _originalValue);
}
