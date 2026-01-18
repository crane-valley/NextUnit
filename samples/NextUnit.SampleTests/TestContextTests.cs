using NextUnit.Core;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating test context functionality.
/// </summary>
public class TestContextTests
{
    private readonly ITestContext _context;

    public TestContextTests(ITestContext context)
    {
        _context = context;
    }

    [Test]
    public void TestContextBasicProperties()
    {
        _context.Output.WriteLine("Test Name: {0}", _context.TestName);
        _context.Output.WriteLine("Class Name: {0}", _context.ClassName);
        _context.Output.WriteLine("Assembly Name: {0}", _context.AssemblyName);
        _context.Output.WriteLine("Fully Qualified Name: {0}", _context.FullyQualifiedName);

        Assert.Equal("TestContextBasicProperties", _context.TestName);
        Assert.Equal("TestContextTests", _context.ClassName);
        Assert.True(_context.AssemblyName.Contains("NextUnit.SampleTests"));
        Assert.True(_context.FullyQualifiedName.Contains("TestContextTests.TestContextBasicProperties"));
    }

    [Test]
    [Category("Integration")]
    [Tag("Smoke")]
    public void TestContextCategoriesAndTags()
    {
        _context.Output.WriteLine("Categories: {0}", string.Join(", ", _context.Categories));
        _context.Output.WriteLine("Tags: {0}", string.Join(", ", _context.Tags));

        Assert.Contains("Integration", _context.Categories);
        Assert.Contains("Smoke", _context.Tags);
    }

    [Test]
    [Arguments(1, "hello", true)]
    [Arguments(42, "world", false)]
    public void TestContextWithArguments(int number, string text, bool flag)
    {
        _context.Output.WriteLine("Arguments count: {0}", _context.Arguments?.Length ?? 0);
        _context.Output.WriteLine("Argument 0: {0}", _context.Arguments?[0]);
        _context.Output.WriteLine("Argument 1: {0}", _context.Arguments?[1]);
        _context.Output.WriteLine("Argument 2: {0}", _context.Arguments?[2]);

        Assert.NotNull(_context.Arguments);
        Assert.Equal(3, _context.Arguments!.Length);
        Assert.Equal(number, _context.Arguments[0]);
        Assert.Equal(text, _context.Arguments[1]);
        Assert.Equal(flag, _context.Arguments[2]);
    }

    [Test]
    public void TestContextStateBag()
    {
        _context.StateBag["key1"] = "value1";
        _context.StateBag["counter"] = 42;
        _context.StateBag["data"] = new[] { 1, 2, 3 };

        Assert.Equal("value1", _context.StateBag["key1"]);
        Assert.Equal(42, _context.StateBag["counter"]);

        _context.Output.WriteLine("StateBag contains {0} items", _context.StateBag.Count);
    }

    [Test]
    public void TestContextCancellationToken()
    {
        _context.Output.WriteLine("CancellationToken.IsCancellationRequested: {0}", _context.CancellationToken.IsCancellationRequested);

        Assert.False(_context.CancellationToken.IsCancellationRequested);
        Assert.True(_context.CancellationToken.CanBeCanceled);
    }

    [Test]
    [Timeout(5000)]
    public void TestContextTimeout()
    {
        _context.Output.WriteLine("TimeoutMs: {0}", _context.TimeoutMs);

        Assert.Equal(5000, _context.TimeoutMs);
    }

    [Test]
    public async Task TestContextAsyncLocalAccessAsync()
    {
        // Access via static TestContext.Current
        var currentContext = TestContext.Current;
        Assert.NotNull(currentContext);

        _context.Output.WriteLine("TestContext.Current accessible: {0}", currentContext != null);
        _context.Output.WriteLine("TestContext.Current.TestName: {0}", currentContext?.TestName);

        // Verify it's the same context (reference equality)
        Assert.True(ReferenceEquals(_context, currentContext), "TestContext.Current should be the same instance as injected context");

        // Verify it works across await boundaries
        await Task.Delay(10);

        var afterAwait = TestContext.Current;
        Assert.NotNull(afterAwait);
        Assert.True(ReferenceEquals(_context, afterAwait), "TestContext.Current should be preserved across await");

        _context.Output.WriteLine("TestContext.Current preserved across await");
    }

    [Test]
    public void TestContextOutputReference()
    {
        // The context's Output property should be functional
        _context.Output.WriteLine("Writing via context.Output");

        Assert.NotNull(_context.Output);
    }
}

/// <summary>
/// Tests demonstrating test context with both ITestContext and ITestOutput constructor injection.
/// </summary>
public class TestContextAndOutputTests
{
    private readonly ITestContext _context;
    private readonly ITestOutput _output;

    public TestContextAndOutputTests(ITestContext context, ITestOutput output)
    {
        _context = context;
        _output = output;
    }

    [Test]
    public void TestWithBothContextAndOutput()
    {
        _output.WriteLine("Direct ITestOutput: Test name = {0}", _context.TestName);
        _context.Output.WriteLine("Via ITestContext.Output: Class name = {0}", _context.ClassName);

        Assert.NotNull(_context);
        Assert.NotNull(_output);
        Assert.Equal("TestWithBothContextAndOutput", _context.TestName);
    }

    [Test]
    [Category("Unit")]
    [Category("Fast")]
    public void TestMultipleCategories()
    {
        _output.WriteLine("Categories: {0}", string.Join(", ", _context.Categories));

        Assert.Contains("Unit", _context.Categories);
        Assert.Contains("Fast", _context.Categories);
        Assert.Equal(2, _context.Categories.Count);
    }
}

/// <summary>
/// Tests demonstrating static TestContext.Current access without constructor injection.
/// </summary>
public class StaticTestContextTests
{
    [Test]
    public void AccessTestContextStatically()
    {
        var context = TestContext.Current;
        Assert.NotNull(context);

        context!.Output.WriteLine("Accessed TestContext.Current statically");
        context.Output.WriteLine("Test Name: {0}", context.TestName);
        context.Output.WriteLine("Class Name: {0}", context.ClassName);

        Assert.Equal("AccessTestContextStatically", context.TestName);
        Assert.Equal("StaticTestContextTests", context.ClassName);
    }

    [Test]
    public async Task AccessTestContextStaticallyAsync()
    {
        var contextBefore = TestContext.Current;
        Assert.NotNull(contextBefore);

        await Task.Delay(10);

        var contextAfter = TestContext.Current;
        Assert.NotNull(contextAfter);
        Assert.True(ReferenceEquals(contextBefore, contextAfter), "TestContext.Current should be preserved across await");

        contextAfter!.Output.WriteLine("TestContext.Current preserved in async context");
    }

    [Test]
    [Arguments(100, 200)]
    public void AccessArgumentsStatically(int a, int b)
    {
        var context = TestContext.Current;
        Assert.NotNull(context);
        Assert.NotNull(context!.Arguments);
        Assert.Equal(2, context.Arguments!.Length);
        Assert.Equal(a, context.Arguments[0]);
        Assert.Equal(b, context.Arguments[1]);
    }
}
