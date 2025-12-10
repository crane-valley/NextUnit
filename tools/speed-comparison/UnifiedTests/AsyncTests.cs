using System.Threading.Tasks;

namespace UnifiedTests;

[TestClass]
public class AsyncTests
{
    [Test]
    public async Task SimpleAsyncTest()
    {
        var result = await ComputeAsync(10);
        var text = await ProcessTextAsync("hello");
        // Simulate realistic async work - benchmark measures overhead
        _ = result + text.Length;
    }

    [Test]
    public async Task ParallelAsyncOperationsTest()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(i => ComputeAsync(i))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var sum = results.Sum();
        // Simulate data processing - benchmark measures parallel overhead
        _ = sum / results.Length;
    }

    [Test]
    public async Task MultipleAsyncPatternsTest()
    {
        var task1 = ComputeAsync(5);
        var task2 = ProcessTextAsync("world");
        
        await Task.WhenAll(task1, task2);
        
        // Simulate result combination - benchmark measures async coordination
        _ = task1.Result + task2.Result.Length;
    }

    private async Task<int> ComputeAsync(int value)
    {
        await Task.Delay(1);
        return value * value;
    }

    private async Task<string> ProcessTextAsync(string text)
    {
        await Task.Delay(1);
        return text.ToUpper();
    }
}
