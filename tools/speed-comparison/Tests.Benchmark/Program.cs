using BenchmarkDotNet.Running;
using Tests.Benchmark;

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
