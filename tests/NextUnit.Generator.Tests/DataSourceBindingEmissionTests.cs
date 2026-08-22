using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins which data source members the generator emits direct access for.
/// </summary>
/// <remarks>
/// Every emitted provider is a direct member reference compiled into the consumer's assembly, so a
/// member the generated registry cannot name, or one whose cancellation token the emitted call has
/// no way to supply, has to produce no provider at all. Emitting one anyway fails the consumer's
/// build inside a file they did not write; emitting none leaves the runtime reflection fallback,
/// which reads non-public members, plus the analyzer diagnostic that names the fix.
/// <para>
/// A source whose declaring type is what the registry cannot name is the exception: its type
/// reference has to go too, which leaves the fallback reflecting over the test class and reading a
/// same-named member there. Those get a provider that throws, so the withheld type is named in a
/// message rather than acted on.
/// </para>
/// </remarks>
public class DataSourceBindingEmissionTests
{
    [Fact]
    public async Task PublicMember_EmitsDirectAccessAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows", registry);
    }

    /// <summary>
    /// The registry is emitted into the assembly being compiled, so internal is in reach and stays
    /// bound. This is the negative half of the accessibility rule.
    /// </summary>
    [Fact]
    public async Task InternalMember_EmitsDirectAccessAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                internal static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows", registry);
    }

    [Fact]
    public async Task PrivateMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        // The name still reaches the runtime, which resolves it reflectively with
        // BindingFlags.NonPublic; only the statically bound provider is withheld.
        Assert.Contains("DataSourceName = \"Rows\",", registry);
        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivateParameterMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static IEnumerable<int> Values => new[] { 1, 2, 3 };

                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """);

        Assert.Contains("MemberProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Values", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// A token-taking member returning a type that implements both element interfaces classifies as
    /// synchronous, and the synchronous provider takes no arguments. Emitting it would produce a
    /// call with no argument for a method that requires one.
    /// </summary>
    [Fact]
    public async Task CancellableDualInterfaceMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public sealed class DualRows : IEnumerable<object[]>, IAsyncEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                public IAsyncEnumerator<object[]> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                    throw new System.NotImplementedException();
            }

            public class DataTests
            {
                public static DualRows Rows(CancellationToken cancellationToken) => new();

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// An explicit <c>MemberType</c> the registry cannot name is withheld from the descriptor as
    /// well as from the provider. Naming it in a <c>typeof</c> would fail the consumer's build with
    /// CS0122, which is the failure NU0020 exists to replace.
    /// </summary>
    [Fact]
    public async Task PrivateMemberType_EmitsNoTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
                }

                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceName = \"Rows\",", registry);

        // The withheld type survives only inside the message, never as a type reference.
        Xunit.Assert.DoesNotContain("typeof(global::TestProject.DataTests.Fixtures)", registry, StringComparison.Ordinal);
        Assert.Contains("DataSourceType = typeof(global::TestProject.DataTests),", registry);

        // A provider that throws, rather than none: with none the runtime would reflect over the
        // test class and a same-named member there would silently supply the wrong rows.
        Assert.Contains("DataSourceProvider = static () => throw new global::System.InvalidOperationException(", registry);
        Assert.Contains("is not accessible from the generated test registry", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The same unreachable <c>MemberType</c> shadowed by a parameter-level source: the test is
    /// bucketed by its combined parameter sources, so no <c>TestDataDescriptor</c> is written for
    /// the member at all -- not even the throwing provider the test above pins.
    /// </summary>
    /// <remarks>
    /// This is the emission fact the <c>NU0020</c> shadowing gate rests on. Nothing in the generated
    /// file names the member, so the rule has no unreachable access to report; if a shadowed member
    /// ever reaches the registry again, the gate in <c>TestDataMemberAnalyzer</c> has to go with it.
    /// </remarks>
    [Fact]
    public async Task ShadowedPrivateMemberType_EmitsNoDescriptorAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
                }

                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes([Values(1, 2)] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("new global::NextUnit.Internal.CombinedDataSourceDescriptor", registry);
        Xunit.Assert.DoesNotContain("DataSourceName = \"Rows\",", registry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("Fixtures", registry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("is not accessible from the generated test registry", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    [Fact]
    public async Task PrivateMemberTypeOnParameterSource_EmitsNoTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<int> Values => new[] { 1, 2, 3 };
                }

                [Test]
                public void Consumes([ValuesFromMember("Values", MemberType = typeof(Fixtures))] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Xunit.Assert.DoesNotContain("typeof(global::TestProject.DataTests.Fixtures)", registry, StringComparison.Ordinal);
        Assert.Contains("MemberType = typeof(global::TestProject.DataTests), ", registry);
        Assert.Contains("MemberProvider = static () => throw new global::System.InvalidOperationException(", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The test class declares a member of the same name. Withholding the named type must not let
    /// either attribute read that one: the runtime resolves a source with no provider by reflecting
    /// over the test class, so a provider that throws is what keeps the wrong rows out.
    /// </summary>
    [Fact]
    public async Task PrivateMemberTypeCollidingWithTestClassMember_DoesNotBindTheTestClassMemberAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                    public static IEnumerable<int> Values => new[] { 1 };
                }

                public static IEnumerable<object[]> Rows => new[] { new object[] { 99 } };

                public static IEnumerable<int> Values => new[] { 99 };

                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }

                [Test]
                public void ConsumesParameter([ValuesFromMember("Values", MemberType = typeof(Fixtures))] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Xunit.Assert.DoesNotContain("global::TestProject.DataTests.Rows", registry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("global::TestProject.DataTests.Values", registry, StringComparison.Ordinal);
        Assert.Contains("DataSourceProvider = static () => throw new global::System.InvalidOperationException(", registry);
        Assert.Contains("MemberProvider = static () => throw new global::System.InvalidOperationException(", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A reachable <c>MemberType</c> is still named, so the withholding is scoped to what would not
    /// compile.
    /// </summary>
    [Fact]
    public async Task InternalMemberType_EmitsTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            internal static class Fixtures
            {
                internal static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests
            {
                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceType = typeof(global::TestProject.Fixtures),", registry);
        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.Fixtures.Rows", registry);
        Assert.Contains("typeof(global::TestProject.Fixtures))]", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A source declared on a base test class is emitted as direct access through the base type,
    /// which is the type resolution bound against. Compiling the output is the half that matters:
    /// the emitted name has to bind the inherited member.
    /// </summary>
    /// <remarks>
    /// The descriptor's <c>DataSourceType</c> stays on the derived type it did before, because the
    /// runtime reads it into the row id prefix. Qualifying the call correctly is not allowed to
    /// rename a single test case.
    /// </remarks>
    [Fact]
    public async Task InheritedMember_EmitsAccessQualifiedByTheDeclaringTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTestsBase.Rows", registry);
        Assert.Contains("DataSourceType = typeof(global::TestProject.DataTests),", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The parameter-level sources reach inherited members through the same walk, and name the
    /// declaring type for the same reason.
    /// </summary>
    [Fact]
    public async Task InheritedParameterMember_EmitsAccessQualifiedByTheDeclaringTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<int> Values => new[] { 1, 2, 3 };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("MemberProvider = static () => (object?)global::TestProject.DataTestsBase.Values", registry);
        Assert.Contains("MemberType = typeof(global::TestProject.DataTests),", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A second source generator adding the same member name to the same partial test class must
    /// not capture the emitted call.
    /// </summary>
    /// <remarks>
    /// Generators cannot see each other's output, so the added member is absent from the
    /// compilation this generator resolves against and present in the one that finally compiles.
    /// The second source here stands in for that output, added after the generator has run. An
    /// access qualified by the type the attribute sits on would bind it silently: the analyzer reads
    /// the same pre-merge compilation, so nothing would report that the rows enumerated are not the
    /// rows validated.
    /// </remarks>
    [Fact]
    public async Task InheritedMember_IsNotCapturedByAConcurrentGeneratorAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public partial class DataTests : DataTestsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTestsBase.Rows", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, ConcurrentlyGeneratedRows);
    }

    /// <summary>
    /// The parameter-level path is a separate emitter branch reading a separate descriptor field, so
    /// it needs its own pin against the same capture.
    /// </summary>
    [Fact]
    public async Task InheritedParameterMember_IsNotCapturedByAConcurrentGeneratorAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<int> Values => new[] { 1, 2, 3 };
            }

            public partial class DataTests : DataTestsBase
            {
                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("MemberProvider = static () => (object?)global::TestProject.DataTestsBase.Values", registry);
        Xunit.Assert.DoesNotContain("DataTests.Values", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, ConcurrentlyGeneratedValues);
    }

    /// <summary>
    /// An inherited member out of reach of the registry is withheld exactly as a local one is, so
    /// the base chain cannot become a way around the accessibility rule.
    /// </summary>
    [Fact]
    public async Task InheritedProtectedMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                protected static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceName = \"Rows\",", registry);
        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// A derived method hides a base member of the same name that is not one, so the base property
    /// must not be bound: the emitted <c>DataTests.Rows</c> would be a method group where a property
    /// read was written, and the consumer's build would fail on generated code. The compile check is
    /// the assertion that matters here.
    /// </summary>
    [Fact]
    public async Task DerivedMethodHidingInheritedProperty_EmitsNoProviderAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                public static new IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The nearest type that declares the name is the only one considered, so a derived
    /// token-taking overload answers the name outright rather than sharing a method group with the
    /// inherited parameterless one. C# would accumulate both and prefer the base overload for a
    /// no-argument call; the contract deliberately does not model that, and binds what the nearest
    /// level offers.
    /// </summary>
    [Fact]
    public async Task DerivedTokenOverload_ShadowsInheritedParameterlessMemberAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                public static async IAsyncEnumerable<object[]> Rows(CancellationToken token)
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new object[] { 2 };
                }

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("AsyncDataSourceProvider = static ct =>", registry);
        Assert.Contains("global::TestProject.DataTests.Rows(ct)", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows()", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A source offering one element type is left inferred. The name would settle nothing there,
    /// and a written type reaches nothing an <c>extern alias</c> hides, where inference needs no
    /// name at all.
    /// </summary>
    [Fact]
    public async Task SingleAsyncEnumerableArm_LeavesTheRowTypeInferredAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public class DataTests
            {
                public static async IAsyncEnumerable<object[]> Rows()
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new object[] { 1 };
                }

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("FromAsyncEnumerableAsync(global::TestProject.DataTests.Rows()", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A source implementing the asynchronous element interface twice used to emit a call whose type
    /// argument could not be inferred, failing the consumer's build with CS0411 in a file they did
    /// not write. The named argument both fixes that and pins which arm is read: TestDataRow&lt;T&gt;
    /// wins by the same precedence rule NU0009 validates against.
    /// </summary>
    [Fact]
    public async Task MultipleAsyncEnumerableArms_NameTheSelectedRowTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public sealed class DualRows : IAsyncEnumerable<object[]>, IAsyncEnumerable<TestDataRow<int>>
            {
                IAsyncEnumerator<object[]> IAsyncEnumerable<object[]>.GetAsyncEnumerator(CancellationToken cancellationToken) =>
                    Untyped().GetAsyncEnumerator(cancellationToken);

                IAsyncEnumerator<TestDataRow<int>> IAsyncEnumerable<TestDataRow<int>>.GetAsyncEnumerator(CancellationToken cancellationToken) =>
                    Typed().GetAsyncEnumerator(cancellationToken);

                private static async IAsyncEnumerable<object[]> Untyped()
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new object[] { 1 };
                }

                private static async IAsyncEnumerable<TestDataRow<int>> Typed()
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new TestDataRow<int>(2);
                }
            }

            public class DataTests
            {
                public static DualRows Rows() => new DualRows();

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains(
            "FromAsyncEnumerableAsync<global::NextUnit.TestDataRow<int>>(global::TestProject.DataTests.Rows()",
            registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The task-wrapped arms are deliberately left inferred. Their type argument is the awaited
    /// collection, not the row, and Task&lt;TRows&gt; admits exactly one inference, so naming it
    /// would move every baseline to state what the compiler had no choice about.
    /// </summary>
    [Fact]
    public async Task TaskWrappedSource_LeavesTheCollectionTypeInferredAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            namespace TestProject;

            public class DataTests
            {
                public static Task<IEnumerable<object[]>> Rows() =>
                    Task.FromResult<IEnumerable<object[]>>(new[] { new object[] { 1 } });

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("FromTaskAsync(global::TestProject.DataTests.Rows()", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A synchronous source implementing the element interface twice is read through the arm
    /// <c>NU0009</c> validated, which is what the typed adapter call is for: the runtime holds the
    /// provider's result as <c>object</c> and reads it back as a non-generic <c>IEnumerable</c>, so
    /// the arm has to be chosen here, where a type argument can still be written.
    /// </summary>
    [Fact]
    public async Task MultipleSyncEnumerableArms_ReadTheSelectedRowTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            namespace TestProject;

            public sealed class DualRows : IEnumerable<object[]>, IEnumerable<TestDataRow<int>>
            {
                IEnumerator<object[]> IEnumerable<object[]>.GetEnumerator() =>
                    ((IEnumerable<object[]>)new[] { new object[] { 1 } }).GetEnumerator();

                IEnumerator<TestDataRow<int>> IEnumerable<TestDataRow<int>>.GetEnumerator() =>
                    ((IEnumerable<TestDataRow<int>>)new[] { new TestDataRow<int>(2) }).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() =>
                    new[] { new object[] { 1 } }.GetEnumerator();
            }

            public class DataTests
            {
                public static DualRows Rows() => new DualRows();

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains(
            "DataSourceProvider = static () => global::NextUnit.Internal.DataSourceAdapter" +
            ".FromEnumerable<global::NextUnit.TestDataRow<int>>(global::TestProject.DataTests.Rows())",
            registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A source offering one element type is handed over as it was. The adapter would only add a
    /// layer between the runtime and the sole arm it was already reading, and the type argument that
    /// selects an arm is also the one thing that can fail to bind.
    /// </summary>
    [Fact]
    public async Task SingleSyncEnumerableArm_KeepsTheDirectReadAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains(
            "DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows()",
            registry);
        Xunit.Assert.DoesNotContain("DataSourceAdapter.FromEnumerable", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A selected row type declared in a reference reachable only through <c>extern alias</c> keeps
    /// the direct read. The generated file carries no alias directive, so the name would bind
    /// nothing there -- <c>CS0400</c> in a file the user did not write -- and this source compiles
    /// today, reading the wrong arm. A wrong row is worth less than a build nobody can fix.
    /// </summary>
    [Fact]
    public async Task MultipleSyncArmsWithAnAliasOnlyRowType_KeepsTheDirectReadAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests
            {
                public static Aliased::Fixtures.DualRows Rows() => new Aliased::Fixtures.DualRows();

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        MetadataReference[] references = [Reference(await CompileDualRowsAssemblyAsync(), "Aliased")];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains(
            "DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows()",
            registry);
        Xunit.Assert.DoesNotContain("DataSourceAdapter.FromEnumerable", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// An unreachable <c>MemberType</c> whose member is declared on a reachable base still emits an
    /// asynchronous access, and it now names that base rather than the test class.
    /// </summary>
    /// <remarks>
    /// The withheld <c>MemberType</c> leaves the descriptor naming the test class, which the
    /// asynchronous access used to be qualified by -- a member that is not declared there, so the
    /// consumer's build failed on generated code with <c>CS0117</c>, or bound a same-named member of
    /// the test class if one existed. Naming the declaring type is what the rest of this change
    /// does, and it happens to be the fix here too.
    /// <para>
    /// The synchronous sibling still throws the <c>NU0020</c> message, because the reflection
    /// fallback behind it would read the test class. The two disagreeing is pre-existing and only
    /// reachable by suppressing <c>NU0020</c>, which fails the build first; it is not settled here.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task UnreachableMemberTypeOverReachableBase_QualifiesTheAsyncAccessByTheDeclaringTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public class PublicRowsBase
            {
                public static async IAsyncEnumerable<object[]> Rows()
                {
                    await System.Threading.Tasks.Task.Yield();
                    yield return new object[] { 1 };
                }
            }

            public class DataTests
            {
                private class PrivateFixtures : PublicRowsBase
                {
                }

                [Test]
                [TestData("Rows", MemberType = typeof(PrivateFixtures))]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains(
            "FromAsyncEnumerableAsync(global::TestProject.PublicRowsBase.Rows()",
            registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows()", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A base declared in a reference reachable only through <c>extern alias</c> cannot qualify the
    /// emitted access. The generated file carries no alias directive, so the base's name binds
    /// nothing there -- or binds a homonym that happens to sit in the global namespace.
    /// </summary>
    [Fact]
    public async Task InheritedFromAnAliasOnlyBase_KeepsTheDerivedQualifierAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests : Aliased::Fixtures.RowsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        MetadataReference[] references = [Reference(await CompileFixtureAssemblyAsync("AliasedFixtures"), "Aliased")];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains("global::TestProject.DataTests.Rows", registry);
        Xunit.Assert.DoesNotContain("global::Fixtures.RowsBase", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// The parameter-level twin: <c>[ValuesFromMember]</c> takes the same qualifier.
    /// </summary>
    [Fact]
    public async Task ParameterMemberInheritedFromAnAliasOnlyBase_KeepsTheDerivedQualifierAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests : Aliased::Fixtures.RowsBase
            {
                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        MetadataReference[] references = [Reference(await CompileFixtureAssemblyAsync("AliasedFixtures"), "Aliased")];

        var registry = await GenerateRegistryAsync(source, references);

        Xunit.Assert.DoesNotContain("global::Fixtures.RowsBase", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// A declaring type the global namespace does hold, but not alone: a second reference declares
    /// the same fully qualified name. The user's source dodges that with the alias and the generated
    /// file cannot, so the emitted access stays on the derived type rather than taking a CS0433.
    /// </summary>
    [Fact]
    public async Task InheritedFromAnAmbiguouslyNamedBase_KeepsTheDerivedQualifierAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests : Aliased::Fixtures.RowsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        MetadataReference[] references =
        [
            Reference(await CompileFixtureAssemblyAsync("AliasedFixtures"), "global", "Aliased"),
            Reference(await CompileFixtureAssemblyAsync("HomonymFixtures")),
        ];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains("global::TestProject.DataTests.Rows", registry);
        Xunit.Assert.DoesNotContain("global::Fixtures.RowsBase", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// One assembly referenced twice, once globally and once under an alias. The base is in the
    /// global namespace and stays the qualifier, which reading the aliases off the reference the
    /// compilation hands back for the assembly would have got backwards: that reference is the
    /// alias-only one, while the type it declares is globally bindable all the same.
    /// </summary>
    [Fact]
    public async Task InheritedFromABaseAlsoReferencedGlobally_QualifiesByTheDeclaringTypeAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests : Aliased::Fixtures.RowsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var image = await CompileFixtureAssemblyAsync("AliasedFixtures");
        MetadataReference[] references = [Reference(image), Reference(image, "Aliased")];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains("global::Fixtures.RowsBase.Rows", registry);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// A base declared in source that a referenced assembly also declares under the same fully
    /// qualified name. C# binds that to the source declaration and warns with <c>CS0436</c>, and the
    /// warning does not travel: the registry's file header is a bare <c>#pragma warning disable</c>,
    /// so the generated file carries none of it even under <c>TreatWarningsAsErrors</c>. So the
    /// qualification holds -- and it has to, because this is a test class deriving from a base of its
    /// own, which is where a concurrent generator has something to capture.
    /// </summary>
    [Fact]
    public async Task InheritedFromASourceBaseShadowingAReference_QualifiesByTheDeclaringTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace Fixtures
            {
                public class RowsBase
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
                }
            }

            namespace TestProject
            {
                public partial class DataTests : global::Fixtures.RowsBase
                {
                    [Test]
                    [TestData("Rows")]
                    public void Consumes(int value)
                    {
                    }
                }
            }
            """;

        MetadataReference[] references = [Reference(await CompileFixtureAssemblyAsync("HomonymFixtures"))];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains("global::Fixtures.RowsBase.Rows", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, ConcurrentlyGeneratedShadowedRows, references);
    }

    /// <summary>
    /// The concurrent generator's member for the shadowed-base case, whose test class declares no
    /// namespace-level partial of its own.
    /// </summary>
    private const string ConcurrentlyGeneratedShadowedRows = """
        using System.Collections.Generic;

        namespace TestProject;

        public partial class DataTests
        {
            public static new IEnumerable<object[]> Rows => new[] { new object[] { 99 } };
        }
        """;

    /// <summary>
    /// The base sits in an alias-hidden namespace whose name a globally referenced <em>type</em> also
    /// carries. The emitted name would compile and read the wrong member: <c>global::Fixtures</c> is
    /// that type, and its nested <c>RowsBase.Rows</c> is not the source anything validated.
    /// </summary>
    [Fact]
    public async Task InheritedFromABaseWhoseNamespaceNameIsATypeElsewhere_KeepsTheDerivedQualifierAsync()
    {
        var source = """
            extern alias Aliased;
            using NextUnit;

            namespace TestProject;

            public class DataTests : Aliased::Fixtures.RowsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        MetadataReference[] references =
        [
            Reference(await CompileFixtureAssemblyAsync("AliasedFixtures"), "Aliased"),
            Reference(await CompileShadowingTypeAssemblyAsync()),
        ];

        var registry = await GenerateRegistryAsync(source, references);

        Assert.Contains("global::TestProject.DataTests.Rows", registry);
        Xunit.Assert.DoesNotContain("global::Fixtures.RowsBase", registry, StringComparison.Ordinal);

        await AssertGeneratedOutputCompilesAsync(source, extraReferences: references);
    }

    /// <summary>
    /// Stands in for a second source generator that adds <c>Rows</c> to the same partial test class.
    /// </summary>
    private const string ConcurrentlyGeneratedRows = """
        using System.Collections.Generic;

        namespace TestProject;

        public partial class DataTests
        {
            public static new IEnumerable<object[]> Rows => new[] { new object[] { 99 } };
        }
        """;

    /// <summary>
    /// The parameter-level counterpart of <see cref="ConcurrentlyGeneratedRows"/>.
    /// </summary>
    private const string ConcurrentlyGeneratedValues = """
        using System.Collections.Generic;

        namespace TestProject;

        public partial class DataTests
        {
            public static new IEnumerable<int> Values => new[] { 99 };
        }
        """;

    /// <summary>
    /// Compiles the user's source together with everything the generator emitted for it. Asserting
    /// on the registry text alone would not catch a type reference emitted somewhere the assertions
    /// do not look, and CS0122 inside generated code is exactly the failure being prevented.
    /// </summary>
    /// <param name="source">The user's source, which is what the generator runs against.</param>
    /// <param name="concurrentlyGeneratedSource">
    /// Another generator's output, added only to the final compilation. Passing it here rather than
    /// to the generator is the whole point: it reproduces a member this generator could not see and
    /// the compiler can.
    /// </param>
    /// <param name="extraReferences">
    /// References the shared set does not carry, given to the generator and to the final
    /// compilation alike so both see the same assemblies the user's project would.
    /// </param>
    private static async Task AssertGeneratedOutputCompilesAsync(
        string source,
        string? concurrentlyGeneratedSource = null,
        IEnumerable<MetadataReference>? extraReferences = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            extraReferences);

        GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _, cancellationToken);

        if (concurrentlyGeneratedSource is not null)
        {
            updated = updated.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                concurrentlyGeneratedSource,
                path: "Concurrent.g.cs",
                cancellationToken: cancellationToken));
        }

        var errors = updated.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.Empty(errors);
    }

    private static async Task<string> GenerateRegistryAsync(
        string source,
        IEnumerable<MetadataReference>? extraReferences = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            extraReferences);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }

    /// <summary>
    /// Compiles a second assembly declaring <c>Fixtures.RowsBase</c> and hands back its image, for
    /// the caller to reference under whichever aliases the case under test needs.
    /// </summary>
    /// <remarks>
    /// The image rather than a reference, because two of these cases need the same assembly, or the
    /// same fully qualified name, referenced twice with different <c>MetadataReferenceProperties</c>.
    /// </remarks>
    private static async Task<ImmutableArray<byte>> CompileFixtureAssemblyAsync(string assemblyName)
    {
        const string source = """
            namespace Fixtures
            {
                public class RowsBase
                {
                    public static System.Collections.Generic.IEnumerable<object[]> Rows =>
                        new[] { new object[] { 1 } };

                    public static System.Collections.Generic.IEnumerable<int> Values =>
                        new[] { 1 };
                }
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var references = await TestReferenceAssemblies.Net10.ResolveAsync(language: null, cancellationToken);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
        Xunit.Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics));

        return ImmutableArray.Create(stream.ToArray());
    }

    /// <summary>
    /// Compiles an assembly declaring a collection that implements the element interface twice, with
    /// the arm the precedence rule selects carrying a row type declared there too.
    /// </summary>
    /// <remarks>
    /// Referenced under an alias only, so the selected row type is the one thing about this source
    /// the generated registry cannot write. <c>Fixtures.AliasedRow</c> wins the tie against
    /// <c>object[]</c> on ordinal comparison of the fully qualified names.
    /// </remarks>
    private static async Task<ImmutableArray<byte>> CompileDualRowsAssemblyAsync()
    {
        const string source = """
            namespace Fixtures
            {
                public class AliasedRow
                {
                }

                public class DualRows :
                    System.Collections.Generic.IEnumerable<AliasedRow>,
                    System.Collections.Generic.IEnumerable<object[]>
                {
                    System.Collections.Generic.IEnumerator<AliasedRow>
                        System.Collections.Generic.IEnumerable<AliasedRow>.GetEnumerator() =>
                        ((System.Collections.Generic.IEnumerable<AliasedRow>)new[] { new AliasedRow() })
                            .GetEnumerator();

                    System.Collections.Generic.IEnumerator<object[]>
                        System.Collections.Generic.IEnumerable<object[]>.GetEnumerator() =>
                        ((System.Collections.Generic.IEnumerable<object[]>)new[] { new object[] { 1 } })
                            .GetEnumerator();

                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                        new[] { new object[] { 1 } }.GetEnumerator();
                }
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var references = await TestReferenceAssemblies.Net10.ResolveAsync(language: null, cancellationToken);
        var compilation = CSharpCompilation.Create(
            "AliasedDualRows",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
        Xunit.Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics));

        return ImmutableArray.Create(stream.ToArray());
    }

    /// <summary>
    /// Compiles an assembly whose global-scope <em>type</em> <c>Fixtures</c> carries the name the
    /// aliased assembly uses for a namespace, with a nested <c>RowsBase.Rows</c> for the emitted
    /// name to bind to if the qualifier is written anyway.
    /// </summary>
    private static async Task<ImmutableArray<byte>> CompileShadowingTypeAssemblyAsync()
    {
        const string source = """
            public class Fixtures
            {
                public class RowsBase
                {
                    public static System.Collections.Generic.IEnumerable<object[]> Rows =>
                        new[] { new object[] { 99 } };
                }
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var references = await TestReferenceAssemblies.Net10.ResolveAsync(language: null, cancellationToken);
        var compilation = CSharpCompilation.Create(
            "ShadowingType",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
        Xunit.Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics));

        return ImmutableArray.Create(stream.ToArray());
    }

    private static MetadataReference Reference(ImmutableArray<byte> image, params string[] aliases) =>
        MetadataReference.CreateFromImage(
            image,
            new MetadataReferenceProperties(aliases: ImmutableArray.Create(aliases)));
}
