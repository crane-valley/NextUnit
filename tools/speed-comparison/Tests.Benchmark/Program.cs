using BenchmarkDotNet.Running;
using Tests.Benchmark;

if (args.Length > 0 && args[0] == "--round-robin")
{
    var rounds = args.Length > 1 ? int.Parse(args[1]) : 20;
    await RoundRobinComparison.RunAsync(rounds);
    return;
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
