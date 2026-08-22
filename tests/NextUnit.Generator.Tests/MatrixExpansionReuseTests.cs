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
        var calls = CallsFromInto(
            "NextUnit.Generator.Emitters.RegistryEmitter",
            "NextUnit.Generator.Helpers.MatrixHelper");

        Assert.Empty(calls);
    }

    /// <summary>
    /// The other half of the same property, and the guard that keeps the test above from passing
    /// because the scan found nothing anywhere: the validator is the one caller of the expansion.
    /// </summary>
    [Fact]
    public void TestCaseExpansionValidator_IsTheOnlyCallerOfTheExpansion()
    {
        var calls = CallsFromInto(
            "NextUnit.Generator.Validators.TestCaseExpansionValidator",
            "NextUnit.Generator.Helpers.MatrixHelper");

        Assert.Equal(
            ["ApplyExclusions", "ComputeCartesianProduct"],
            calls.Select(static call => call.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray());
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

    private static IReadOnlyList<MethodBase> CallsFromInto(string callerTypeName, string calleeTypeName)
    {
        var generator = typeof(NextUnitGenerator).Assembly;
        var caller = generator.GetType(callerTypeName, throwOnError: true)!;
        var callee = generator.GetType(calleeTypeName, throwOnError: true)!;

        return DeclaredMethods(caller)
            .SelectMany(CalledMethods)
            .Where(called => called.DeclaringType == callee)
            .ToList();
    }

    /// <summary>
    /// Every method the type itself declares, including the ones the compiler moved into nested
    /// closure classes for the lambdas the emitter passes around.
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

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var method in DeclaredMethods(nested))
            {
                yield return method;
            }
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
