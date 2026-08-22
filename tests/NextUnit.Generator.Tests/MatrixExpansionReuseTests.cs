using System.Reflection;
using System.Reflection.Emit;
using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins that a matrix method is expanded once per compilation, and that the count the cap charges is
/// the number of test cases the registry then contains.
/// </summary>
/// <remarks>
/// The expansion is a pure function of the descriptor, so running it once and running it twice
/// produce byte-identical generated text: there is no output-only way to tell the two apart, and the
/// pin has to look at the seam instead. It looks at compiled IL rather than at the seam's types
/// because <c>InternalsVisibleTo</c> is not available to this project -- <c>NextUnit.Shared</c> is
/// linked into both <c>NextUnit.Core</c> and <c>NextUnit.Generator</c>, so making the generator's
/// internals visible here makes <c>TestCaseExpansionPolicy</c> ambiguous (CS0433) in every test that
/// already names it.
/// </remarks>
public sealed class MatrixExpansionReuseTests
{
    private const string ExpansionLimitId = "NEXTUNIT013";
    private const string MatrixHelperTypeName = "NextUnit.Generator.Helpers.MatrixHelper";
    private const string EmitterTypeName = "NextUnit.Generator.Emitters.RegistryEmitter";
    private const string ValidatorTypeName = "NextUnit.Generator.Validators.TestCaseExpansionValidator";

    /// <summary>
    /// Thirteen surviving combinations repeated twice, from sixteen before the exclusions.
    /// </summary>
    private const string ExcludedMatrixSource = """
        using NextUnit;

        namespace TestProject;

        public class MatrixTests
        {
            [Test]
            [MatrixExclusion(1, 1)]
            [MatrixExclusion(2, 2)]
            [MatrixExclusion(3, 3)]
            [Repeat(2)]
            public void Combined([Matrix(1, 2, 3, 4)] int p0, [Matrix(1, 2, 3, 4)] int p1)
            {
            }
        }
        """;

    private static readonly IReadOnlyDictionary<short, OpCode> _opCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(static field => field.FieldType == typeof(OpCode))
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static opCode => opCode.Value);

    /// <summary>
    /// The emitter writes out the expansion it is handed. A call back into <c>MatrixHelper</c> here
    /// would be a second Cartesian product and a second exclusion pass -- exclusions x combinations
    /// x parameter width, with nothing bounding the exclusion count -- for an answer the validator
    /// already computed.
    /// </summary>
    [Fact]
    public void RegistryEmitter_DoesNotExpandTheMatrixAgain()
    {
        Assert.DoesNotContain(EmitterTypeName, ExpansionCallSites().Select(static site => site.Caller));
    }

    /// <summary>
    /// The whole generator holds two call sites into the expansion, both in the validator, one per
    /// helper. Counted rather than deduplicated, and scanned across every type rather than the two
    /// named above: a second expansion added beside the first, or moved into a third type, is the
    /// regression this is here to catch, and either would survive a check that only asked which
    /// distinct helpers some named type calls.
    /// </summary>
    [Fact]
    public void TheExpansion_HasExactlyOneCallSitePerHelper()
    {
        Assert.Equal(
            [
                (ValidatorTypeName, "ApplyExclusions"),
                (ValidatorTypeName, "ComputeCartesianProduct"),
            ],
            ExpansionCallSites());
    }

    /// <summary>
    /// At the boundary the cap admits, the registry holds exactly as many test cases as the
    /// projection counted -- exclusions applied, then repeated.
    /// </summary>
    [Fact]
    public async Task ExclusionsAtTheCapBoundary_EmitOneCasePerProjectedCaseAsync()
    {
        var (registry, diagnostics) = await GenerateAsync(ExcludedMatrixSource, configuredCap: "26");

        Assert.Empty(diagnostics);
        Assert.Equal(26, CountTestCases(registry));
    }

    /// <summary>
    /// One below that boundary the same method is rejected, and the number in the message is the
    /// number of cases the test above found in the registry. The peak of the running product is 16,
    /// under both caps, so what decides this case is the post-exclusion count alone.
    /// </summary>
    [Fact]
    public async Task ExclusionsOneOverTheCapBoundary_ReportTheEmittedCountAsync()
    {
        var (registry, diagnostics) = await GenerateAsync(ExcludedMatrixSource, configuredCap: "25");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ExpansionLimitId, diagnostic.Id);
        Xunit.Assert.Contains("expands to 26 test cases", diagnostic.GetMessage());

        // Reporting is only half of it: an over-limit method is dropped rather than emitted.
        Assert.Equal(0, CountTestCases(registry));
    }

    private static async Task<(string Registry, ImmutableArray<Diagnostic> Diagnostics)> GenerateAsync(
        string source,
        string configuredCap)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);

        var optionsProvider = new GeneratorDriverHarness.GlobalOptionsProvider(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.NextUnitMaxTestCasesPerMethod"] = configuredCap,
            });

        var result = GeneratorDriverHarness
            .CreateDriver(trackIncrementalGeneratorSteps: false, optionsProvider)
            .RunGenerators(compilation, cancellationToken)
            .GetRunResult();

        var registry = result.Results
            .SelectMany(static generatorResult => generatorResult.GeneratedSources)
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();

        return (registry, result.Diagnostics);
    }

    private static int CountTestCases(string registry)
    {
        // The generator always emits LF, on every host OS; the trailing newline is what separates
        // the descriptor construction from the array type of the property, which reads the same.
        const string marker = "new global::NextUnit.Internal.TestCaseDescriptor\n";
        var count = 0;

        for (var index = registry.IndexOf(marker, StringComparison.Ordinal);
            index >= 0;
            index = registry.IndexOf(marker, index + marker.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Every call site in the generator that reaches <c>MatrixHelper</c>, named by the type that
    /// declares the calling method and the helper it calls.
    /// </summary>
    /// <remarks>
    /// The helper's own internal calls are dropped: <c>ApplyExclusions</c> reaching its matcher is
    /// the one expansion doing its work, not a second one.
    /// </remarks>
    private static IReadOnlyList<(string Caller, string Method)> ExpansionCallSites()
    {
        var generator = typeof(NextUnitGenerator).Assembly;
        var helper = generator.GetType(MatrixHelperTypeName, throwOnError: true)!;

        return generator.GetTypes()
            .SelectMany(DeclaredMethods)
            .SelectMany(static method => CalledMethods(method).Select(called => (Caller: method, Called: called)))
            .Where(site => site.Called.DeclaringType == helper)
            .Select(static site => (Caller: OutermostTypeName(site.Caller.DeclaringType!), site.Called.Name))
            .Where(static site => site.Caller != MatrixHelperTypeName)
            .OrderBy(static site => site.Caller, StringComparer.Ordinal)
            .ThenBy(static site => site.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The type a compiler-generated closure class belongs to, so a call moved into a lambda is
    /// still attributed to the type whose source contains it.
    /// </summary>
    private static string OutermostTypeName(Type type)
    {
        while (type.DeclaringType is { } declaring)
        {
            type = declaring;
        }

        return type.FullName!;
    }

    /// <summary>
    /// Every method the type itself declares. Nested types are not walked, because the assembly-wide
    /// scan reaches them on their own and would otherwise count their call sites twice.
    /// </summary>
    private static IEnumerable<MethodBase> DeclaredMethods(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            yield return method;
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            yield return constructor;
        }
    }

    /// <summary>
    /// Walks one method body instruction by instruction and resolves every call target.
    /// </summary>
    /// <remarks>
    /// The walk decodes operand lengths rather than scanning for call opcodes directly, because an
    /// operand byte can hold the same value as an opcode and a naive scan would resolve garbage.
    /// </remarks>
    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();

        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType?.GetGenericArguments();
        var methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
        var offset = 0;

        while (offset < il.Length)
        {
            short value = il[offset++];

            if (value == 0xFE)
            {
                value = (short)(0xFE00 | il[offset++]);
            }

            var opCode = _opCodesByValue[value];

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var resolved = method.Module.ResolveMethod(
                    BitConverter.ToInt32(il, offset), typeArguments, methodArguments);

                if (resolved is not null)
                {
                    yield return resolved;
                }
            }

            offset += OperandSize(opCode, il, offset);
        }
    }

    private static int OperandSize(OpCode opCode, byte[] il, int offset) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
            OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
            OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
        _ => throw new NotSupportedException($"Unhandled IL operand type {opCode.OperandType}."),
    };
}
