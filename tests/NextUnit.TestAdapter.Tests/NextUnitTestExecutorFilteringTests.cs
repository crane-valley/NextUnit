using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;
using Xunit;
using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace NextUnit.TestAdapter.Tests
{
    [CollectionDefinition("ExecutorFiltering", DisableParallelization = true)]
    public sealed class ExecutorFilteringCollection
    {
    }

    [Collection("ExecutorFiltering")]
    public sealed class NextUnitTestExecutorFilteringTests : IDisposable
    {
        private static string Source => typeof(NextUnitTestExecutorFilteringTests).Assembly.Location;

        public void Dispose() => Generated.GeneratedTestRegistry.Clear();

        [Fact]
        public void SourceRun_RejectAllFilterDoesNotRunTestBodies()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false);
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            var contextState = (InvestigationRunContext)context;
            var handleState = (InvestigationFrameworkHandle)handle;
            Assert.Equal(1, contextState.FilterRequests);
            Assert.Equal(2, contextState.FilterMatches);
            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Empty(handleState.Results);
            Assert.Empty(handleState.Messages);
            Assert.Contains("Category", contextState.SupportedProperties);
            Assert.Contains("Tag", contextState.SupportedProperties);
            Assert.Equal("Category", contextState.PropertyResolver!("category")!.Label);
            Assert.Null(contextState.PropertyResolver("UnknownProperty"));
        }

        [Theory]
        [InlineData("FullyQualifiedName")]
        [InlineData("DisplayName")]
        [InlineData("Category")]
        [InlineData("Tag")]
        public void SourceRun_MatchesExpandedRowIdentityAndMetadata(string property)
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false, typedRow: true);
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            var expected = property switch
            {
                "FullyQualifiedName" => $"Investigation.Dynamic:{typeof(InvestigationTarget).FullName}.FirstRows[0]",
                "DisplayName" => "row-only display name",
                "Category" => "RowCategory",
                _ => "RowTag"
            };
            ((InvestigationRunContext)context).Matcher = (_, values) =>
                values(property) is string value ? value == expected :
                values(property) is string[] labels && labels.Contains(expected);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal("row-only display name", Assert.Single(((InvestigationFrameworkHandle)handle).Results).TestCase.DisplayName);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void SourceRun_InvalidFilterReportsErrorBeforeProviderOrBodyExecution()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false);
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            ((InvestigationRunContext)context).FilterFailure = new TestPlatformFormatException("Unsupported filter property");
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            Assert.Equal(0, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
            var message = Assert.Single(((InvestigationFrameworkHandle)handle).Messages);
            Assert.Equal(TestMessageLevel.Error, message.Level);
            Assert.Contains("Unsupported filter property", message.Message);
        }

        [Fact]
        public void SourceRun_FilterMatchFailureReportsErrorAndDoesNotRunBodies()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false);
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            ((InvestigationRunContext)context).Matcher = (_, _) => throw new InvalidOperationException("Filter evaluation failed");
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
            var message = Assert.Single(((InvestigationFrameworkHandle)handle).Messages);
            Assert.Equal(TestMessageLevel.Error, message.Level);
            Assert.Contains("Filter evaluation failed", message.Message);
        }

        [Fact]
        public void SourceRun_NullContextPreservesUnfilteredExecution()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, null, handle);

            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal(2, ((InvestigationFrameworkHandle)handle).Results.Count);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void FilterProperties_DoNotAdvertiseExplicitOptIn()
        {
            Xunit.Assert.DoesNotContain("Explicit", VSTestCaseFactory.SupportedFilterProperties);
            Xunit.Assert.DoesNotContain("ExplicitReason", VSTestCaseFactory.SupportedFilterProperties);
            Assert.Null(VSTestCaseFactory.GetFilterProperty("Explicit"));
            Assert.Null(VSTestCaseFactory.GetFilterProperty("explicitreason"));
        }

        [Fact]
        public void SourceRun_ExcludesExplicitCasesBeforeProviderExecution()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false, explicitTests: true);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, null, handle);

            Assert.Equal(0, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void SelectedExplicitCase_PreservesExplicitSelectionOptIn()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false, explicitTests: true);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var selected = new TestCase("Investigation.Ordinary", new Uri(NextUnitTestExecutor.ExecutorUri), Source);

            new NextUnitTestExecutor().RunTests(new[] { selected }, null, handle);

            Assert.Equal(0, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed, Assert.Single(((InvestigationFrameworkHandle)handle).Results).Outcome);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void SourceRun_ExcludedPrerequisiteReportsMissingDependencyWithoutRunningBodies()
        {
            Generated.GeneratedTestRegistry.ResetDependencyCases();
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            ((InvestigationRunContext)context).Matcher = (_, values) =>
                values("FullyQualifiedName") is string id && id != "Investigation.Prerequisite";
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            Assert.Empty(Generated.GeneratedTestRegistry.DependencyExecutions);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
            var message = Assert.Single(((InvestigationFrameworkHandle)handle).Messages);
            Assert.Equal(TestMessageLevel.Error, message.Level);
            Assert.Contains("Missing dependency Investigation.Prerequisite for Dependent", message.Message);
        }

        [Fact]
        public void SourceRun_CompleteDependencySelectionRunsPrerequisiteBeforeDependent()
        {
            Generated.GeneratedTestRegistry.ResetDependencyCases();
            var context = DispatchProxy.Create<IRunContext, InvestigationRunContext>();
            ((InvestigationRunContext)context).Matcher = (_, _) => true;
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();

            new NextUnitTestExecutor().RunTests(new[] { Source }, context, handle);

            var executionOrder = Generated.GeneratedTestRegistry.DependencyExecutions.ToList();
            Assert.Equal(3, executionOrder.Count);
            Assert.Contains("Prerequisite", executionOrder);
            Assert.Contains("Dependent", executionOrder);
            Assert.Contains("Independent", executionOrder);
            Assert.True(executionOrder.IndexOf("Prerequisite") < executionOrder.IndexOf("Dependent"));
            var results = ((InvestigationFrameworkHandle)handle).Results;
            Assert.Equal(3, results.Count);
            Xunit.Assert.All(results, result => Assert.Equal(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed, result.Outcome));
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void SelectedCases_MissingPrerequisitePreservesSameFailClosedPolicy()
        {
            Generated.GeneratedTestRegistry.ResetDependencyCases();
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var selected = new[] { "Investigation.Dependent", "Investigation.Independent" }
                .Select(id => new TestCase(id, new Uri(NextUnitTestExecutor.ExecutorUri), Source));

            new NextUnitTestExecutor().RunTests(selected, null, handle);

            Assert.Empty(Generated.GeneratedTestRegistry.DependencyExecutions);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
            var message = Assert.Single(((InvestigationFrameworkHandle)handle).Messages);
            Assert.Equal(TestMessageLevel.Error, message.Level);
            Assert.Contains("Missing dependency Investigation.Prerequisite for Dependent", message.Message);
        }

        [Fact]
        public void SelectedDifferentMethod_DoesNotExpandUnselectedProvider()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: false);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var selected = new TestCase("Investigation.Ordinary", new Uri(NextUnitTestExecutor.ExecutorUri), Source);

            new NextUnitTestExecutor().RunTests(new[] { selected }, null, handle);

            Assert.Equal(0, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed, Assert.Single(((InvestigationFrameworkHandle)handle).Results).Outcome);
        }

        [Fact]
        public void SelectedRow_DoesNotExpandUnselectedProviderOfSameMethod()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: true);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var rowId = $"Investigation.Dynamic:{typeof(InvestigationTarget).FullName}.FirstRows[0]";
            var selected = new TestCase(rowId, new Uri(NextUnitTestExecutor.ExecutorUri), Source);

            new NextUnitTestExecutor().RunTests(new[] { selected }, null, handle);

            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.SecondProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.OrdinaryBodyCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed, Assert.Single(((InvestigationFrameworkHandle)handle).Results).Outcome);
        }

        [Theory]
        [InlineData(false, true, 1)]
        [InlineData(true, false, 1)]
        [InlineData(true, true, 2)]
        public void SelectedDeferredOrRepeatedRow_PreservesSourceSelection(bool deferred, bool repeated, int expectedBodies)
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: true, deferred: deferred, repeated: repeated);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var groupId = $"Investigation.Dynamic:{typeof(InvestigationTarget).FullName}.FirstRows";
            var selectedId = deferred && !repeated ? groupId : $"{groupId}[0]{(repeated ? "#1" : "")}";
            var selected = new TestCase(selectedId, new Uri(NextUnitTestExecutor.ExecutorUri), Source);

            new NextUnitTestExecutor().RunTests(new[] { selected }, null, handle);

            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(0, Generated.GeneratedTestRegistry.SecondProviderCalls);
            Assert.Equal(expectedBodies, Generated.GeneratedTestRegistry.DynamicBodyCalls);
            Assert.Equal(expectedBodies, ((InvestigationFrameworkHandle)handle).Results.Count);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Messages);
        }

        [Fact]
        public void SelectedBaseId_PreservesDescriptorExpansionFallback()
        {
            Generated.GeneratedTestRegistry.Reset(includeSecondProvider: true);
            var handle = DispatchProxy.Create<IFrameworkHandle, InvestigationFrameworkHandle>();
            var selected = new TestCase("Investigation.Dynamic", new Uri(NextUnitTestExecutor.ExecutorUri), Source);

            new NextUnitTestExecutor().RunTests(new[] { selected }, null, handle);

            Assert.Equal(1, Generated.GeneratedTestRegistry.FirstProviderCalls);
            Assert.Equal(1, Generated.GeneratedTestRegistry.SecondProviderCalls);
            Assert.Empty(((InvestigationFrameworkHandle)handle).Results);
        }
    }

    public class InvestigationRunContext : DispatchProxy
    {
        public int FilterRequests { get; private set; }
        public int FilterMatches { get; private set; }
        public string[] SupportedProperties { get; private set; } = [];
        public Func<string, TestProperty?>? PropertyResolver { get; private set; }
        public Func<TestCase, Func<string, object?>, bool>? Matcher { get; set; }
        public Exception? FilterFailure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "GetTestCaseFilter")
            {
                FilterRequests++;
                if (FilterFailure is not null)
                {
                    throw FilterFailure;
                }

                SupportedProperties = ((IEnumerable<string>)args![0]!).ToArray();
                PropertyResolver = (Func<string, TestProperty?>)args[1]!;
                var filter = DispatchProxy.Create<ITestCaseFilterExpression, InvestigationRejectAllFilter>();
                ((InvestigationRejectAllFilter)filter).OnMatch = () => FilterMatches++;
                ((InvestigationRejectAllFilter)filter).Matcher = Matcher;
                return filter;
            }

            return targetMethod?.ReturnType == typeof(bool) ? false : null;
        }
    }

    public class InvestigationRejectAllFilter : DispatchProxy
    {
        public Action? OnMatch { get; set; }
        public Func<TestCase, Func<string, object?>, bool>? Matcher { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "MatchTestCase")
            {
                OnMatch?.Invoke();
                return Matcher?.Invoke((TestCase)args![0]!, (Func<string, object?>)args[1]!) ?? false;
            }

            return "FullyQualifiedName=NoSuchTest";
        }
    }

    public class InvestigationFrameworkHandle : DispatchProxy
    {
        public ConcurrentBag<TestResult> Results { get; } = [];
        public ConcurrentBag<(TestMessageLevel Level, string Message)> Messages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "RecordResult" && args?[0] is TestResult result)
            {
                Results.Add(result);
            }
            else if (targetMethod?.Name == "SendMessage")
            {
                Messages.Add(((TestMessageLevel)args![0]!, (string)args[1]!));
            }

            return targetMethod?.ReturnType == typeof(bool) ? false : null;
        }
    }

    public sealed class InvestigationTarget
    {
    }
}

namespace NextUnit.Generated
{
    public static class GeneratedTestRegistry
    {
        private static int _firstProviderCalls;
        private static int _secondProviderCalls;
        private static int _ordinaryBodyCalls;
        private static int _dynamicBodyCalls;
        public static int FirstProviderCalls => Volatile.Read(ref _firstProviderCalls);
        public static int SecondProviderCalls => Volatile.Read(ref _secondProviderCalls);
        public static int OrdinaryBodyCalls => Volatile.Read(ref _ordinaryBodyCalls);
        public static int DynamicBodyCalls => Volatile.Read(ref _dynamicBodyCalls);
        public static IReadOnlyList<TestCaseDescriptor> TestCases { get; private set; } = [];
        public static IReadOnlyList<TestDataDescriptor> TestDataDescriptors { get; private set; } = [];
        public static ConcurrentQueue<string> DependencyExecutions { get; } = [];

        public static void Clear()
        {
            TestCases = [];
            TestDataDescriptors = [];
            DependencyExecutions.Clear();
        }

        public static void ResetDependencyCases()
        {
            Clear();
            TestCases =
            [
                CreateDependencyCase("Prerequisite"),
                CreateDependencyCase("Dependent", "Prerequisite"),
                CreateDependencyCase("Independent")
            ];
        }

        private static TestCaseDescriptor CreateDependencyCase(string name, params string[] prerequisites) => new()
        {
            Id = new TestCaseId($"Investigation.{name}"),
            DisplayName = name,
            Dependencies = prerequisites.Select(prerequisite => new TestCaseId($"Investigation.{prerequisite}")).ToArray(),
            TestClass = typeof(TestAdapter.Tests.InvestigationTarget),
            TestClassFactory = static (_, _) => new TestAdapter.Tests.InvestigationTarget(),
            TestMethod = (_, _) =>
            {
                DependencyExecutions.Enqueue(name);
                return Task.CompletedTask;
            }
        };

        public static void Reset(bool includeSecondProvider, bool typedRow = false, bool deferred = false, bool repeated = false, bool explicitTests = false)
        {
            _firstProviderCalls = 0;
            _secondProviderCalls = 0;
            _ordinaryBodyCalls = 0;
            _dynamicBodyCalls = 0;
            TestCases =
            [
                new TestCaseDescriptor
                {
                    Id = new TestCaseId("Investigation.Ordinary"),
                    DisplayName = "Ordinary",
                    IsExplicit = explicitTests,
                    TestClass = typeof(TestAdapter.Tests.InvestigationTarget),
                    TestClassFactory = static (_, _) => new TestAdapter.Tests.InvestigationTarget(),
                    TestMethod = static (_, _) =>
                    {
                        Interlocked.Increment(ref _ordinaryBodyCalls);
                        return Task.CompletedTask;
                    }
                }
            ];
            var descriptors = new List<TestDataDescriptor> { CreateDataDescriptor("FirstRows", false, typedRow, deferred, repeated, explicitTests) };
            if (includeSecondProvider)
            {
                descriptors.Add(CreateDataDescriptor("SecondRows", true, typedRow, deferred, repeated, explicitTests));
            }

            TestDataDescriptors = descriptors;
        }

        private static TestDataDescriptor CreateDataDescriptor(string memberName, bool second, bool typedRow, bool deferred, bool repeated, bool explicitTests) => new()
        {
            BaseId = "Investigation.Dynamic",
            DisplayName = "Dynamic",
            IsExplicit = explicitTests,
            TestClass = typeof(TestAdapter.Tests.InvestigationTarget),
            MethodName = "Dynamic",
            DataSourceName = memberName,
            DeferredEnumeration = deferred,
            RepeatCount = repeated ? 2 : null,
            TestClassFactory = static (_, _) => new TestAdapter.Tests.InvestigationTarget(),
            DataSourceProvider = () =>
            {
                if (second)
                {
                    Interlocked.Increment(ref _secondProviderCalls);
                }
                else
                {
                    Interlocked.Increment(ref _firstProviderCalls);
                }

                return typedRow
                    ? new object[] { new TestDataRow<int>(42, displayName: "row-only display name", categories: ["RowCategory"], tags: ["RowTag"]) }
                    : new[] { new object[] { 42 } };
            },
            TestMethodWithArguments = static (_, _, _) =>
            {
                Interlocked.Increment(ref _dynamicBodyCalls);
                return Task.CompletedTask;
            }
        };
    }
}
