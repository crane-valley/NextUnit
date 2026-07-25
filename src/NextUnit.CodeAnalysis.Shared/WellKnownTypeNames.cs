namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// BCL type names the source generator and the analyzers both match against.
/// </summary>
internal static class WellKnownTypeNames
{
    /// <summary>
    /// Display name of <c>CancellationToken</c>, matched against
    /// <c>ITypeSymbol.ToDisplayString()</c> to detect a trailing cancellation parameter.
    /// </summary>
    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string Task = "System.Threading.Tasks.Task";
    public const string ValueTask = "System.Threading.Tasks.ValueTask";
    public const string GenericValueTask = "System.Threading.Tasks.ValueTask`1";
}
