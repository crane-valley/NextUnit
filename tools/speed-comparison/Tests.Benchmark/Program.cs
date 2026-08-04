using BenchmarkDotNet.Running;
using Tests.Benchmark;

if (args.Length > 0)
{
    switch (args[0])
    {
        case "--round-robin":
            await RoundRobinComparison.RunAsync(args.Length > 1 ? int.Parse(args[1]) : 21);
            return 0;
        case "--analyze-regression":
            return await RegressionCommand.AnalyzeAsync(args);
        case "--append-history":
            return await RegressionCommand.AppendAsync(args);
        default:
            break;
    }
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

var output = new DirectoryInfo(Environment.CurrentDirectory)
    .GetFiles("*.md", SearchOption.AllDirectories)
    .OrderByDescending(x => x.LastWriteTime)
    .FirstOrDefault();

var file = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

if (!string.IsNullOrEmpty(file) && output != null)
{
    await File.WriteAllTextAsync(file, await File.ReadAllTextAsync(output.FullName));
}

return 0;
