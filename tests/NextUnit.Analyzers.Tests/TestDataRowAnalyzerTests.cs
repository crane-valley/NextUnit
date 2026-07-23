using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public sealed class TestDataRowAnalyzerTests
{
    [Fact]
    public async Task ConstantValidMetadata_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public static class Rows
            {
                public static TestDataRow<int> Row => new(
                    1,
                    displayName: "one",
                    categories: ["Valid"],
                    tags: ["Fast"],
                    skipReason: "Tracked issue");
            }
            """;

        await CSharpAnalyzerVerifier<TestDataRowAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WhitespaceDisplayName_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public static class Rows
            {
                public static TestDataRow<int> Row => new(1, displayName: {|#0:" "|});
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataRowAnalyzer>
            .Diagnostic("NU0010")
            .WithLocation(0)
            .WithArguments("displayName");

        await CSharpAnalyzerVerifier<TestDataRowAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task InvalidCategoryAndTag_ReportsDiagnosticsAsync()
    {
        var source = """
            using NextUnit;

            public static class Rows
            {
                public static TestDataRow<int> Row => new(
                    1,
                    categories: ["Valid", {|#0:""|}],
                    tags: [{|#1:null!|}]);
            }
            """;

        var expectedCategory = CSharpAnalyzerVerifier<TestDataRowAnalyzer>
            .Diagnostic("NU0010")
            .WithLocation(0)
            .WithArguments("categories");
        var expectedTag = CSharpAnalyzerVerifier<TestDataRowAnalyzer>
            .Diagnostic("NU0010")
            .WithLocation(1)
            .WithArguments("tags");

        await CSharpAnalyzerVerifier<TestDataRowAnalyzer>.VerifyAnalyzerAsync(
            source,
            expectedCategory,
            expectedTag);
    }
}
