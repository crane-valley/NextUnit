using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using NextUnit.Analyzers.Analyzers;
using NextUnit.Generator;

namespace NextUnit.Docs.Tests;

/// <summary>
/// Compiles every NextUnit code sample in the migration guides.
/// </summary>
/// <remarks>
/// <para>
/// The guides are the source of truth. Samples are extracted from the Markdown itself rather than
/// mirrored in a parallel project, so a published sample cannot drift from a compiled one.
/// </para>
/// <para>
/// The extracted samples run through the NextUnit source generator and the NextUnit analyzers as
/// well as the compiler, which proves they are usable tests rather than merely valid C#: a data
/// source without <c>[Test]</c>, a misnamed <c>[TestData]</c> member, or an unreachable retry policy
/// is reported here exactly as it would be in a reader's project.
/// </para>
/// </remarks>
public class MigrationGuideSampleTests
{
    /// <summary>
    /// Guides whose NextUnit samples are compiled. <c>MIGRATION_FROM_XUNIT.md</c> is absent because
    /// its samples are fragments rather than compilation units; see PLANS.md.
    /// </summary>
    private static readonly string[] _guides =
    [
        "MIGRATION_FROM_NUNIT.md",
        "MIGRATION_FROM_MSTEST.md",
    ];

    /// <summary>
    /// Fence languages the guides may use. An unlisted language fails the check, so a typo such as
    /// <c>cs</c> or <c>cshrap</c> cannot quietly remove a sample from compilation.
    /// </summary>
    private static readonly string[] _knownLanguages = ["bash", "csharp", "json", "text", "xml"];

    /// <summary>
    /// Info-string annotations marking a C# block as another framework's code, shown for comparison
    /// and therefore not compiled here. Any other annotation is rejected, because a typo must not be
    /// able to silently drop a NextUnit sample out of this check.
    /// </summary>
    private static readonly string[] _foreignFrameworks = ["nunit", "mstest", "xunit"];

    private static readonly string[] _sampleUsings =
    [
        "using System;",
        "using System.Collections.Generic;",
        "using System.IO;",
        "using System.Linq;",
        "using System.Net.Http;",
        "using System.Threading;",
        "using System.Threading.Tasks;",
    ];

    private static readonly Lazy<ImmutableArray<MetadataReference>> _references = new(CreateReferences);

    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> _analyzers = new(CreateAnalyzers);

    /// <summary>
    /// A guide that stopped contributing samples would otherwise pass silently, so require a floor.
    /// </summary>
    private const int MinimumSamplesPerGuide = 10;

    /// <summary>
    /// The category both the NextUnit analyzers and the NextUnit generator stamp on their
    /// diagnostics.
    /// </summary>
    private const string NextUnitCategory = "NextUnit";

    [Fact]
    public async Task EveryNextUnitSampleCompilesAsync()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var samples = _guides.SelectMany(LoadBlocks).Where(IsNextUnitSample).ToArray();
        var trees = samples.Select(block => ParseSample(block, cancellationToken)).ToArray();

        var compilation = CSharpCompilation.Create(
            "MigrationGuideSamples",
            trees,
            _references.Value,
            new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new NextUnitGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generated,
            out var driverDiagnostics,
            cancellationToken);

        var analyzerDiagnostics = await generated
            .WithAnalyzers(_analyzers.Value)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        var failures = generated.GetDiagnostics(cancellationToken)
            .AddRange(driverDiagnostics)
            .AddRange(analyzerDiagnostics)
            .Where(IsFailure)
            .Select(Describe)
            .ToArray();

        Xunit.Assert.True(
            failures.Length == 0,
            $"Migration guide samples failed to compile:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public void EveryGuideContributesSamples()
    {
        foreach (var guide in _guides)
        {
            var samples = LoadBlocks(guide).Count(IsNextUnitSample);

            Xunit.Assert.True(
                samples >= MinimumSamplesPerGuide,
                $"{guide} contributes {samples} compiled samples, fewer than the expected {MinimumSamplesPerGuide}. " +
                "Either the guide lost its samples or the extractor stopped recognizing them.");
        }
    }

    [Fact]
    public void CodeFenceInfoStringsAreRecognized()
    {
        foreach (var block in _guides.SelectMany(LoadBlocks))
        {
            Xunit.Assert.True(
                _knownLanguages.Contains(block.Language, StringComparer.Ordinal),
                $"{block.Location}: unknown fence language '{block.Language}'. " +
                $"Expected one of {string.Join(", ", _knownLanguages)}.");

            if (block.Language != "csharp")
            {
                continue;
            }

            Xunit.Assert.True(
                block.Annotations.Count <= 1,
                $"{block.Location}: a C# block takes at most one annotation, found '{block.InfoString}'.");

            foreach (var annotation in block.Annotations)
            {
                Xunit.Assert.True(
                    _foreignFrameworks.Contains(annotation, StringComparer.Ordinal),
                    $"{block.Location}: unknown annotation '{annotation}'. " +
                    $"Expected one of {string.Join(", ", _foreignFrameworks)}, or none for a compiled NextUnit sample.");
            }
        }
    }

    /// <summary>
    /// A compiler error always fails. A NextUnit diagnostic fails from warning severity upwards,
    /// because several of them -- a data source without <c>[Test]</c>, an unresolved
    /// <c>[DependsOn]</c> target -- ship as warnings, and a published sample that trips one is wrong
    /// even though a reader's build would only warn. Non-NextUnit warnings are left alone so the
    /// samples are not dragged toward this repository's own analyzer settings.
    /// </summary>
    private static bool IsFailure(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error ||
        (diagnostic.Severity == DiagnosticSeverity.Warning &&
            string.Equals(diagnostic.Descriptor.Category, NextUnitCategory, StringComparison.Ordinal));

    private static bool IsNextUnitSample(MarkdownCodeBlock block) =>
        block.Language == "csharp" && block.Annotations.Count == 0;

    private static IReadOnlyList<MarkdownCodeBlock> LoadBlocks(string guide)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Guides", guide);
        return MarkdownCodeBlocks.Parse(guide, File.ReadAllText(path));
    }

    private static SyntaxTree ParseSample(MarkdownCodeBlock block, CancellationToken cancellationToken)
    {
        // The wrapper namespace deliberately does not start with "NextUnit": a sample nested under
        // that namespace would resolve NextUnit types without a using directive, and the check would
        // stop catching a sample that forgets one.
        var containerNamespace = $"MigrationGuideSamples.{Identifier(block.DocumentName)}.Line{block.FenceLine}";
        var prefix = $"{string.Join('\n', _sampleUsings)}\n\nnamespace {containerNamespace}\n{{\n";

        return CSharpSyntaxTree.ParseText(
            $"{prefix}{block.Code}\n}}\n",
            path: SamplePath(block),
            cancellationToken: cancellationToken);
    }

    private static string Describe(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var sample = ParseSamplePath(span.Path);

        if (sample is null)
        {
            return $"generated code: {diagnostic.Id}: {diagnostic.GetMessage()}";
        }

        // Map the position back to the document so the failure names a line the author can open.
        var prefixLines = $"{string.Join('\n', _sampleUsings)}\n\nnamespace x\n{{\n".Count(character => character == '\n');
        var documentLine = sample.Value.FenceLine + 1 + (span.StartLinePosition.Line - prefixLines);

        return $"{sample.Value.DocumentName} line {documentLine}: {diagnostic.Id}: {diagnostic.GetMessage()}";
    }

    private static string SamplePath(MarkdownCodeBlock block) => $"{block.DocumentName}#{block.FenceLine}";

    private static (string DocumentName, int FenceLine)? ParseSamplePath(string path)
    {
        var separator = path.LastIndexOf('#');

        return separator > 0 && int.TryParse(path[(separator + 1)..], out var fenceLine)
            ? (path[..separator], fenceLine)
            : null;
    }

    private static string Identifier(string documentName) =>
        new(Path.GetFileNameWithoutExtension(documentName)
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());

    private static ImmutableArray<DiagnosticAnalyzer> CreateAnalyzers() =>
    [
        .. typeof(MissingTestAttributeAnalyzer).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!),
    ];

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        // The reference set is the shared framework plus what the NextUnit package brings, which is
        // what a reader's project has. Referencing everything the test host loaded would let a
        // sample compile against xUnit or Roslyn and still be published as NextUnit-only code.
        var sharedFramework = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException("The shared framework directory could not be resolved.");

        if (PathsMatch(sharedFramework, AppContext.BaseDirectory))
        {
            throw new InvalidOperationException(
                "The shared framework resolves to the test output directory, so the reference set " +
                "cannot be narrowed to what a NextUnit consumer has. Run this check on a " +
                "framework-dependent build.");
        }

        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable, so samples cannot be compiled.");

        // The trusted list is the managed subset of the framework directory; enumerating the
        // directory instead would pick up native libraries that carry no metadata.
        var candidates = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Where(path => PathsMatch(Path.GetDirectoryName(path), sharedFramework))
            .Concat(
            [
                typeof(NextUnit.TestAttribute).Assembly.Location,
                typeof(NextUnit.Platform.NextUnitApplicationBuilderExtensions).Assembly.Location,
                typeof(TestApplication).Assembly.Location,
            ]);

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            if (seen.Add(Path.GetFileName(path)))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return references.ToImmutable();
    }

    private static bool PathsMatch(string? left, string? right) =>
        left is not null &&
        right is not null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
