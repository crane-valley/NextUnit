namespace NextUnit.Generator.Models;

internal sealed record TestClassConstructorMetadata
{
    public TestClassConstructorMetadata(
        TestClassConstructorKind kind,
        bool requiresTestOutput,
        bool requiresTestContext)
    {
        Kind = kind;
        RequiresTestOutput = requiresTestOutput;
        RequiresTestContext = requiresTestContext;
    }

    public TestClassConstructorKind Kind { get; }

    public bool RequiresTestOutput { get; }

    public bool RequiresTestContext { get; }
}
