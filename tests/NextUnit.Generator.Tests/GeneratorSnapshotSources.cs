namespace NextUnit.Generator.Tests;

/// <summary>
/// Input sources for the generated-code snapshot tests.
/// </summary>
/// <remarks>
/// Each constant is paired with a baseline file under <c>Snapshots/</c> named after the
/// constant. Keep the sources minimal: they exist to pin one emission path each.
/// </remarks>
internal static class GeneratorSnapshotSources
{
    public const string PlainTest = """
        using NextUnit;

        namespace TestProject;

        public class PlainTests
        {
            [Test]
            public void SimpleTest()
            {
            }
        }
        """;

    public const string ArgumentsTest = """
        using NextUnit;

        namespace TestProject;

        public class ArgumentTests
        {
            [Test]
            [Arguments(1, 2)]
            [Arguments(3, 4)]
            public void Add(int a, int b)
            {
            }
        }
        """;

    public const string MatrixTest = """
        using NextUnit;

        namespace TestProject;

        public class MatrixTests
        {
            [Test]
            [MatrixExclusion(1, 20)]
            public void Combine(
                [Matrix(1, 2)] int a,
                [Matrix(10, 20)] int b)
            {
            }
        }
        """;

    public const string TestDataTest = """
        using System.Collections.Generic;
        using NextUnit;

        namespace TestProject;

        public class TestDataTests
        {
            public static IEnumerable<object[]> Rows()
            {
                yield return new object[] { 1, 2 };
            }

            [Test]
            [TestData(nameof(Rows))]
            public void DataDriven(int a, int b)
            {
            }
        }
        """;

    /// <summary>
    /// Pins every asynchronous <c>[TestData]</c> shape the generator binds, including the
    /// cancellation-aware method form.
    /// </summary>
    public const string AsyncTestDataTest = """
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using NextUnit;

        namespace TestProject;

        public class AsyncTestDataTests
        {
            public static async IAsyncEnumerable<object[]> StreamedRows(
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                await Task.Yield();
                yield return new object[] { 1, 2 };
            }

            public static async IAsyncEnumerable<object[]> UncancellableRows()
            {
                await Task.Yield();
                yield return new object[] { 3, 4 };
            }

            public static Task<IEnumerable<object[]>> TaskRows() =>
                Task.FromResult<IEnumerable<object[]>>(new[] { new object[] { 5, 6 } });

            public static ValueTask<IReadOnlyList<object[]>> ValueTaskRows() =>
                new ValueTask<IReadOnlyList<object[]>>(new[] { new object[] { 7, 8 } });

            [Test]
            [TestData(nameof(StreamedRows))]
            [TestData(nameof(UncancellableRows))]
            [TestData(nameof(TaskRows))]
            [TestData(nameof(ValueTaskRows))]
            public void DataDriven(int a, int b)
            {
            }
        }
        """;

    public const string ClassDataSourceTest = """
        using System.Collections;
        using System.Collections.Generic;
        using NextUnit;

        namespace TestProject;

        public class AdditionData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { 1, 2 };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class ClassDataSourceTests
        {
            [Test]
            [ClassDataSource<AdditionData>]
            public void DataDriven(int a, int b)
            {
            }
        }
        """;

    public const string CombinedDataSourceTest = """
        using System.Collections.Generic;
        using NextUnit;

        namespace TestProject;

        public class CombinedTests
        {
            public static IEnumerable<int> Numbers()
            {
                yield return 1;
                yield return 2;
            }

            [Test]
            public void Combine(
                [ValuesFromMember(nameof(Numbers))] int number,
                [Values("a", "b")] string label)
            {
            }
        }
        """;

    public const string LifecycleScopesTest = """
        using NextUnit;

        namespace TestProject;

        public class LifecycleTests
        {
            [Before(LifecycleScope.Test)]
            public void BeforeTest()
            {
            }

            [After(LifecycleScope.Test)]
            public void AfterTest()
            {
            }

            [Before(LifecycleScope.Class)]
            public static void BeforeClass()
            {
            }

            [After(LifecycleScope.Class)]
            public static void AfterClass()
            {
            }

            [Before(LifecycleScope.Assembly)]
            public static void BeforeAssembly()
            {
            }

            [After(LifecycleScope.Assembly)]
            public static void AfterAssembly()
            {
            }

            [Before(LifecycleScope.Session)]
            public static void BeforeSession()
            {
            }

            [After(LifecycleScope.Session)]
            public static void AfterSession()
            {
            }

            [Test]
            public void SimpleTest()
            {
            }
        }
        """;

    public const string DependencyMetadataTest = """
        using NextUnit;

        namespace TestProject;

        public class ExternalTests
        {
            [Test]
            public void External()
            {
            }
        }

        public class DependencyTests
        {
            [Test]
            public void First()
            {
            }

            [Test]
            public void Second()
            {
            }

            [Test]
            [DependsOn(nameof(First), nameof(Second))]
            [DependsOn(nameof(First), ProceedOnFailure = true)]
            [DependsOn("TestProject.ExternalTests.External", ProceedOnFailure = true)]
            public void Dependent()
            {
            }
        }
        """;

    public const string ConstructorInjectionTest = """
        using NextUnit;
        using NextUnit.Core;

        namespace TestProject;

        public class ParameterlessTests
        {
            [Test]
            public void Test()
            {
            }
        }

        public class ContextTests
        {
            public ContextTests(ITestContext context)
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class OutputTests
        {
            public OutputTests(ITestOutput output)
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class ContextAndOutputTests
        {
            public ContextAndOutputTests(ITestContext context, ITestOutput output)
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class OutputAndContextTests
        {
            public OutputAndContextTests(ITestOutput output, ITestContext context)
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class StaticOnlyTests
        {
            private StaticOnlyTests()
            {
            }

            [Test]
            public static void Test()
            {
            }
        }

        public class PrivateOnlyTests
        {
            private PrivateOnlyTests()
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class MultipleOneArgumentConstructorsTests
        {
            public MultipleOneArgumentConstructorsTests(ITestOutput output)
            {
            }

            public MultipleOneArgumentConstructorsTests(ITestContext context)
            {
            }

            [Test]
            public void Test()
            {
            }
        }

        public class MultipleTwoArgumentConstructorsTests
        {
            public MultipleTwoArgumentConstructorsTests(ITestOutput output, ITestContext context)
            {
            }

            public MultipleTwoArgumentConstructorsTests(ITestContext context, ITestOutput output)
            {
            }

            [Test]
            public void Test()
            {
            }
        }
        """;

    public const string UserEntryPointTest = """
        using System.Threading.Tasks;
        using NextUnit;

        namespace TestProject;

        public class EntryPointTests
        {
            [Test]
            public void SimpleTest()
            {
            }
        }

        public static class Program
        {
            public static Task<int> Main(string[] args) => Task.FromResult(args.Length);
        }
        """;
}
