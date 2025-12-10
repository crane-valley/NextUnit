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
        var combined = result + text.Length;
    }

    [Test]
    public async Task ParallelAsyncOperationsTest()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(i => ComputeAsync(i))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var sum = results.Sum();
        var average = sum / results.Length;
    }

    [Test]
    public async Task MultipleAsyncPatternsTest()
    {
        var task1 = ComputeAsync(5);
        var task2 = ProcessTextAsync("world");
        
        await Task.WhenAll(task1, task2);
        
        var result = task1.Result + task2.Result.Length;
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
