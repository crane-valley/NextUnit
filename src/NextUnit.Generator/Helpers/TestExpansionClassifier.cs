using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Which expansion a test method gets, and therefore which registry property it is emitted into.
/// </summary>
internal enum TestExpansionKind
{
    /// <summary>One test case per <c>[Arguments]</c> set, or one for a method with none.</summary>
    Regular,

    /// <summary>One test case per surviving <c>[Matrix]</c> combination.</summary>
    Matrix,

    /// <summary>One descriptor per <c>[TestData]</c> source, expanded at discovery.</summary>
    TestData,

    /// <summary>One descriptor per method, expanded from <c>[ClassDataSource]</c> at discovery.</summary>
    ClassDataSource,

    /// <summary>One descriptor per method, expanded from parameter-level sources at discovery.</summary>
    CombinedDataSource,
}

/// <summary>
/// Decides which expansion one test method gets.
/// </summary>
/// <remarks>
/// A method may carry several data-source kinds; exactly one of them expands, and the generator
/// reports the combination as a diagnostic separately. The order of the checks is that precedence.
/// <para>
/// Both readers of the decision call this rather than repeating the order:
/// <c>TestCaseExpansionValidator</c> to charge the right expansion against the cap, and
/// <c>RegistryEmitter</c> to pick the bucket. They have to agree because the matrix expansion is
/// now computed by the first and consumed by the second -- two copies of the precedence that
/// drifted apart would hand the emitter no combinations and drop that method's test cases in
/// silence, where drift used to cost only a wrong projected count.
/// </para>
/// </remarks>
internal static class TestExpansionClassifier
{
    public static TestExpansionKind Classify(TestMethodDescriptor test)
    {
        if (!test.CombinedParameterSources.IsDefaultOrEmpty)
        {
            return TestExpansionKind.CombinedDataSource;
        }

        if (!test.ClassDataSources.IsDefaultOrEmpty)
        {
            return TestExpansionKind.ClassDataSource;
        }

        if (!test.TestDataSources.IsDefaultOrEmpty)
        {
            return TestExpansionKind.TestData;
        }

        if (!test.MatrixParameters.IsDefaultOrEmpty)
        {
            return TestExpansionKind.Matrix;
        }

        return TestExpansionKind.Regular;
    }
}
