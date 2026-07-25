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
