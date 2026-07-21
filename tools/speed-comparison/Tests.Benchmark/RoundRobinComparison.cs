using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tests.Benchmark;

internal static class RoundRobinComparison
{
    private const int ExpectedTestCount = 127;
    private static readonly string[] _frameworkNames = ["NextUnit", "TUnit", "NUnit", "MSTest", "xUnit"];

    public static async Task RunAsync(int rounds)
    {
        if (rounds <= 0 || rounds % _frameworkNames.Length != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rounds),
                $"Rounds must be a positive multiple of {_frameworkNames.Length} so every framework occupies every run position equally.");
        }

        var repositoryRoot = FindRepositoryRoot();
        var unifiedProject = Path.Join(repositoryRoot, "tools", "speed-comparison", "UnifiedTests", "UnifiedTests.csproj");
        var frameworks = LoadFrameworks(repositoryRoot, unifiedProject);

        foreach (var framework in frameworks)
        {
            await BuildAsync(repositoryRoot, unifiedProject, framework.Id);
        }

        foreach (var framework in frameworks)
        {
            await RunTestProcessAsync(framework.ExecutablePath);
        }

        var measurements = new List<Measurement>(rounds * frameworks.Count);
        for (var round = 0; round < rounds; round++)
        {
            for (var position = 0; position < frameworks.Count; position++)
            {
                var framework = frameworks[(round + position) % frameworks.Count];
                var stopwatch = Stopwatch.StartNew();
                await RunTestProcessAsync(framework.ExecutablePath);
                stopwatch.Stop();
                measurements.Add(new Measurement(round + 1, position + 1, framework.Name, stopwatch.Elapsed.TotalMilliseconds));
            }
        }

        var summaries = frameworks
            .Select(framework => Summarize(framework, measurements))
            .ToList();
        var nextUnitMedian = summaries.Single(summary => summary.Framework == "NextUnit").MedianMilliseconds;
        summaries = summaries
            .Select(summary => summary with { RelativeToNextUnit = summary.MedianMilliseconds / nextUnitMedian })
            .OrderBy(summary => summary.MedianMilliseconds)
            .ToList();

        var result = new ComparisonResult(
            DateTimeOffset.UtcNow,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            await CaptureProcessOutputAsync(repositoryRoot, "dotnet", "--version"),
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            rounds,
            ExpectedTestCount,
            "Cyclic round-robin; one untimed warm-up per framework; standalone MTP executables; stdout/stderr redirected; --no-progress --no-ansi; telemetry disabled; TUnit HTML report disabled.",
            summaries,
            measurements);

        var resultsDirectory = Path.Join(repositoryRoot, "tools", "speed-comparison", "results");
        Directory.CreateDirectory(resultsDirectory);
        var jsonPath = Path.Join(resultsDirectory, "runtime-comparison.json");
        var markdownPath = Path.Join(resultsDirectory, "RUNTIME_COMPARISON.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        await File.WriteAllTextAsync(markdownPath, RenderMarkdown(result));

        Console.WriteLine(RenderMarkdown(result));
        Console.WriteLine($"Raw measurements: {jsonPath}");
    }

    private static List<Framework> LoadFrameworks(string repositoryRoot, string unifiedProject)
    {
        var projectContent = File.ReadAllText(unifiedProject);
        var nextUnitContent = File.ReadAllText(Path.Join(repositoryRoot, "Directory.Build.props"));
        var executableName = OperatingSystem.IsWindows() ? "UnifiedTests.exe" : "UnifiedTests";

        return
        [
            CreateFramework("NEXTUNIT", "NextUnit", ReadProperty(nextUnitContent, "Version"), executableName, repositoryRoot),
            CreateFramework("TUNIT", "TUnit", ReadProperty(projectContent, "TUnitVersion"), executableName, repositoryRoot),
            CreateFramework("NUNIT", "NUnit", ReadProperty(projectContent, "NUnitVersion"), executableName, repositoryRoot),
            CreateFramework("MSTEST", "MSTest", ReadProperty(projectContent, "MSTestVersion"), executableName, repositoryRoot),
            CreateFramework("XUNIT", "xUnit", ReadProperty(projectContent, "XUnitVersion"), executableName, repositoryRoot)
        ];
    }

    private static Framework CreateFramework(
        string id,
        string name,
        string version,
        string executableName,
        string repositoryRoot)
    {
        var executablePath = Path.Join(
            repositoryRoot,
            "tools",
            "speed-comparison",
            "UnifiedTests",
            "bin",
            $"Release-{id}",
            "net10.0",
            executableName);
        return new Framework(id, name, version, executablePath);
    }

    private static string ReadProperty(string content, string propertyName)
    {
        var match = Regex.Match(content, $@"<{Regex.Escape(propertyName)}>([^<]+)</{Regex.Escape(propertyName)}>");
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"MSBuild property {propertyName} was not found.");
    }

    private static async Task BuildAsync(string repositoryRoot, string projectPath, string framework)
    {
        var output = await CaptureProcessOutputAsync(
            repositoryRoot,
            "dotnet",
            "build",
            projectPath,
            "--configuration",
            "Release",
            $"-p:TestFramework={framework}",
            "--framework",
            "net10.0",
            "--verbosity",
            "quiet");
        Console.Write(output);
    }

    private static async Task RunTestProcessAsync(string executablePath)
    {
        var startInfo = CreateProcessStartInfo(Path.GetDirectoryName(executablePath)!, executablePath, "--no-progress", "--no-ansi");
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["TESTINGPLATFORM_UI_LANGUAGE"] = "en-us";
        startInfo.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["TUNIT_DISABLE_HTML_REPORTER"] = "true";

        var output = await RunProcessAsync(startInfo);
        if (!output.Contains($"total: {ExpectedTestCount}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected {ExpectedTestCount} tests from {executablePath}.{Environment.NewLine}{output}");
        }
    }

    private static async Task<string> CaptureProcessOutputAsync(string workingDirectory, string fileName, params string[] arguments)
    {
        return (await RunProcessAsync(CreateProcessStartInfo(workingDirectory, fileName, arguments))).Trim();
    }

    private static ProcessStartInfo CreateProcessStartInfo(string workingDirectory, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task<string> RunProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await standardOutput) + (await standardError);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process {startInfo.FileName} exited with code {process.ExitCode}.{Environment.NewLine}{output}");
        }

        return output;
    }

    private static FrameworkSummary Summarize(Framework framework, IReadOnlyCollection<Measurement> measurements)
    {
        var values = measurements
            .Where(measurement => measurement.Framework == framework.Name)
            .Select(measurement => measurement.ElapsedMilliseconds)
            .Order()
            .ToArray();
        var mean = values.Average();
        var median = values.Length % 2 == 0
            ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2
            : values[values.Length / 2];
        var variance = values.Sum(value => Math.Pow(value - mean, 2)) / (values.Length - 1);
        return new FrameworkSummary(
            framework.Name,
            framework.Version,
            values.Length,
            mean,
            median,
            Math.Sqrt(variance),
            values[0],
            values[^1],
            0);
    }

    private static string RenderMarkdown(ComparisonResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Round-robin runtime comparison");
        builder.AppendLine();
        builder.AppendLine($"Generated: {result.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();
        builder.AppendLine($"Environment: {result.OperatingSystem} ({result.Architecture})");
        builder.AppendLine();
        builder.AppendLine($".NET SDK / Runtime: {result.DotNetSdkVersion} / {result.Runtime}");
        builder.AppendLine();
        builder.AppendLine($"Processor: {result.Processor}");
        builder.AppendLine();
        builder.AppendLine($"Workload: {result.ExpectedTestCount} tests, {result.Rounds} measured runs per framework");
        builder.AppendLine();
        builder.AppendLine("| Framework | Version | Runs | Mean | Median | StdDev | Min | Max | Median / NextUnit |");
        builder.AppendLine("| --------- | ------- | ---: | ---: | -----: | -----: | --: | --: | ----------------: |");
        foreach (var summary in result.Summaries)
        {
            builder.AppendLine(
                $"| {summary.Framework} | {summary.Version} | {summary.Runs} | {summary.MeanMilliseconds:F2}ms | {summary.MedianMilliseconds:F2}ms | {summary.StandardDeviationMilliseconds:F2}ms | {summary.MinimumMilliseconds:F2}ms | {summary.MaximumMilliseconds:F2}ms | {summary.RelativeToNextUnit:F2}x |");
        }

        builder.AppendLine();
        builder.AppendLine("Method:");
        builder.AppendLine();
        builder.AppendLine("- Cyclic round-robin with one untimed warm-up per framework.");
        builder.AppendLine("- Standalone MTP executables with stdout and stderr redirected.");
        builder.AppendLine("- Common `--no-progress --no-ansi` arguments and telemetry disabled.");
        builder.AppendLine("- TUnit HTML report generation disabled.");
        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "NextUnit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record Framework(string Id, string Name, string Version, string ExecutablePath);
    private sealed record Measurement(int Round, int Position, string Framework, double ElapsedMilliseconds);
    private sealed record FrameworkSummary(
        string Framework,
        string Version,
        int Runs,
        double MeanMilliseconds,
        double MedianMilliseconds,
        double StandardDeviationMilliseconds,
        double MinimumMilliseconds,
        double MaximumMilliseconds,
        double RelativeToNextUnit);
    private sealed record ComparisonResult(
        DateTimeOffset GeneratedAtUtc,
        string OperatingSystem,
        string Architecture,
        string Runtime,
        string DotNetSdkVersion,
        string Processor,
        int Rounds,
        int ExpectedTestCount,
        string Methodology,
        IReadOnlyList<FrameworkSummary> Summaries,
        IReadOnlyList<Measurement> Measurements);
}
