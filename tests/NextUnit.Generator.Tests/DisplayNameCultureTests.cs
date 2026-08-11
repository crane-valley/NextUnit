using System.Globalization;
using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins that what the generator bakes into the registry - both the display name and the C# literal
/// an argument is emitted as - is formatted with the invariant culture, so the same source produces
/// the same registry on every build machine.
/// </summary>
/// <remarks>
/// Asserted through the driver rather than against the formatters directly, because they are
/// internal to the generator and the property that matters is the text that reaches the registry.
/// </remarks>
public sealed class DisplayNameCultureTests
{
    private const string NegativeArgumentsSource = """
        using NextUnit;

        namespace TestProject;

        public enum Direction
        {
            Backward = -1,
            Forward = 1
        }

        public class DisplayNameTests
        {
            [Test]
            [Arguments(-5, -2.5, -1L, Direction.Backward)]
            public void Negate(int count, double ratio, long ticks, Direction direction)
            {
            }
        }
        """;

    [Fact]
    public async Task NegativeArguments_AreDisplayedWithTheInvariantCultureAsync()
    {
        using var ambient = AmbientCulture.Set("sv-SE");
        AssertTheAmbientCultureWouldDifferOnItsOwn();

        var registry = await GenerateRegistryAsync(NegativeArgumentsSource);

        Xunit.Assert.Contains(
            """DisplayName = "Negate(-5, -2.5, -1, Direction.-1)",""",
            registry,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeArguments_ProduceCompilableSourceAsync()
    {
        using var ambient = AmbientCulture.Set("sv-SE");
        AssertTheAmbientCultureWouldDifferOnItsOwn();

        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            NegativeArgumentsSource,
            OutputKind.ConsoleApplication,
            cancellationToken);

        GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _, cancellationToken);

        // A literal emitted with sv-SE's U+2212 negative sign is not a number to the C# lexer, so
        // this fails on the emitted source rather than on the display name.
        var errors = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Xunit.Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    /// Guards both tests against passing on a machine where the ambient culture would have produced
    /// the expected text anyway, which would make them prove nothing.
    /// </summary>
    private static void AssertTheAmbientCultureWouldDifferOnItsOwn()
    {
        Xunit.Assert.NotEqual("-5", (-5).ToString(CultureInfo.CurrentCulture));
        Xunit.Assert.NotEqual("-2.5", (-2.5).ToString(CultureInfo.CurrentCulture));
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }

    /// <summary>
    /// Pins the ambient culture for the duration of a test and puts back what was there.
    /// </summary>
    private readonly struct AmbientCulture : IDisposable
    {
        private readonly CultureInfo _culture;

        private AmbientCulture(CultureInfo culture)
        {
            _culture = culture;
        }

        public static AmbientCulture Set(string name)
        {
            var scope = new AmbientCulture(CultureInfo.CurrentCulture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            return scope;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
