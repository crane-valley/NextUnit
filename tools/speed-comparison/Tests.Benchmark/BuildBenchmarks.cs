using BenchmarkDotNet.Attributes;
using Cysharp.Diagnostics;

namespace Tests.Benchmark;

[BenchmarkCategory("Build")]
public class BuildBenchmarks : BenchmarkBase
{
    [Benchmark]
    public async Task BuildNextUnitAsync()
    {
        var command = $"dotnet build --no-incremental -c Release -p:TestFramework=NEXTUNIT --framework {Framework}";
        await foreach (var output in ProcessX.StartAsync(command, workingDirectory: UnifiedPath))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task BuildNUnitAsync()
    {
        var command = $"dotnet build --no-incremental -c Release -p:TestFramework=NUNIT --framework {Framework}";
        await foreach (var output in ProcessX.StartAsync(command, workingDirectory: UnifiedPath))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task BuildMSTestAsync()
    {
        var command = $"dotnet build --no-incremental -c Release -p:TestFramework=MSTEST --framework {Framework}";
        await foreach (var output in ProcessX.StartAsync(command, workingDirectory: UnifiedPath))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task BuildXUnitAsync()
    {
        var command = $"dotnet build --no-incremental -c Release -p:TestFramework=XUNIT --framework {Framework}";
        await foreach (var output in ProcessX.StartAsync(command, workingDirectory: UnifiedPath))
        {
            Console.WriteLine(output);
        }
    }
}
