using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins that every emitted type name survives a type whose identifier is a C# keyword.
/// </summary>
/// <remarks>
/// The registry is compiled as part of the consumer's build, so an unescaped <c>global::event</c>
/// fails to parse there and points the error at a file the user did not write.
/// </remarks>
public sealed class KeywordIdentifierEmissionTests
{
    /// <summary>
    /// Covers every emission path that carries a user-declared type name: the test class, the data
    /// source type (both the class source and the member source), the parameter type, and the enum
    /// type behind an <c>[Arguments]</c> value.
    /// </summary>
    private const string KeywordNamedTypesSource = """
        using System.Collections;
        using System.Collections.Generic;
        using NextUnit;

        namespace TestProject;

        public sealed class @event
        {
            public int Value { get; set; }
        }

        public sealed class @return : IEnumerable<object?[]>
        {
            public IEnumerator<object?[]> GetEnumerator()
            {
                yield return new object?[] { new @event { Value = 1 } };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static class @static
        {
            public static IEnumerable<object?[]> Rows()
            {
                yield return new object?[] { new @event { Value = 2 } };
            }
        }

        public enum @int
        {
            Zero = 0,
        }

        public sealed class @class
        {
            [Test]
            [ClassDataSource<@return>]
            public void FromClassSource(@event value)
            {
            }

            [Test]
            [TestData(nameof(@static.Rows), MemberType = typeof(@static))]
            public void FromMemberSource(@event value)
            {
            }

            [Test]
            [Arguments(@int.Zero)]
            public void FromArguments(@int value)
            {
            }
        }
        """;

    [Fact]
    public async Task KeywordNamedTypes_EmitEscapedIdentifiersAsync()
    {
        var registry = await GenerateRegistryAsync(KeywordNamedTypesSource);

        Xunit.Assert.Contains("global::TestProject.@class", registry, StringComparison.Ordinal);
        Xunit.Assert.Contains("global::TestProject.@return", registry, StringComparison.Ordinal);
        Xunit.Assert.Contains("global::TestProject.@static", registry, StringComparison.Ordinal);
        Xunit.Assert.Contains("typeof(global::TestProject.@event)", registry, StringComparison.Ordinal);
        Xunit.Assert.Contains("(global::TestProject.@int)0", registry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeywordNamedTypes_ProduceCompilableRegistryAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            KeywordNamedTypesSource,
            OutputKind.ConsoleApplication,
            cancellationToken);

        GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _, cancellationToken);

        var errors = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Xunit.Assert.Empty(errors);
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.ConsoleApplication,
            cancellationToken);

        return GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken)
            .GetRunResult()
            .Results
            .Single()
            .GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }
}
