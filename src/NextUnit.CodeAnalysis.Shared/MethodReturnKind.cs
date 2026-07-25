namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Classification of a test or lifecycle method's return type.
/// </summary>
internal enum MethodReturnKind
{
    Void,
    Task,
    ValueTask,
    Unsupported
}
