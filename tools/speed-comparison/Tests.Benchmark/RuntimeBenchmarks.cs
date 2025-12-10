using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using CliWrap;
using CliWrap.Buffered;

namespace Tests.Benchmark;

[BenchmarkCategory("Runtime")]
public class RuntimeBenchmarks : BenchmarkBase
{
    private static readonly string? ClassName = Environment.GetEnvironmentVariable("CLASS_NAME");
    private string? _aotPath;
    private string? _nextUnitPath;
    private string? _nunitPath;
    private string? _msTestPath;
    private string? _xUnitPath;

    [GlobalSetup]
    public void Setup()
    {
        // Validate and cache executable paths
        var exeName = GetExecutableFileName();

        // Regular builds
        _nextUnitPath = GetExecutablePath("NEXTUNIT", exeName);
        _nunitPath = GetExecutablePath("NUNIT", exeName);
        _msTestPath = GetExecutablePath("MSTEST", exeName);
        _xUnitPath = GetExecutablePath("XUNIT", exeName);

        // AOT build (different path - in publish folder)
        var aotExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UnifiedTests.exe" : "UnifiedTests";
        var aotPath = Path.Combine(UnifiedPath, "bin", "Release-NEXTUNIT", Framework, "publish");
        _aotPath = Path.Combine(aotPath, aotExeName);

        // Build missing executables automatically
        BuildIfMissing("NEXTUNIT", _nextUnitPath);
        BuildIfMissing("NUNIT", _nunitPath);
        BuildIfMissing("MSTEST", _msTestPath);
        BuildIfMissing("XUNIT", _xUnitPath);
    }

    private string GetExecutablePath(string framework, string exeName)
    {
        var binPath = Path.Combine(UnifiedPath, "bin", $"Release-{framework}", Framework);
        return Path.Combine(binPath, exeName);
    }

    private void BuildIfMissing(string framework, string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            Console.WriteLine($"Building {framework} executable at {executablePath}...");
            var result = Cli.Wrap("dotnet")
                .WithArguments(["build", "-c", "Release", "-p:TestFramework=" + framework, "--framework", Framework, "--verbosity", "quiet"])
                .WithWorkingDirectory(UnifiedPath)
                .ExecuteBufferedAsync()
                .GetAwaiter()
                .GetResult();

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to build {framework}: {result.StandardError}");
            }

            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException($"{framework} executable not found at {executablePath} after build.");
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Runtime", "AOT")]
    public async Task NextUnit_AOT()
    {
        // Build AOT version if it doesn't exist
        // We do this in the benchmark method rather than Setup because:
        // 1. AOT builds are very slow and should be optional
        // 2. Not all users will want to run AOT benchmarks
        if (!File.Exists(_aotPath))
        {
            Console.WriteLine($"AOT executable not found. Building AOT version (this may take several minutes)...");
            var result = await Cli.Wrap("dotnet")
                .WithArguments(["publish", "-c", "Release", "-p:TestFramework=NEXTUNIT", "-p:PublishAot=true", "--framework", Framework])
                .WithWorkingDirectory(UnifiedPath)
                .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to build AOT version: {result.StandardError}");
            }

            if (!File.Exists(_aotPath))
            {
                throw new InvalidOperationException($"AOT executable not found at {_aotPath} after publish. You may need to run 'dotnet publish -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true' manually.");
            }
        }

        await Cli.Wrap(_aotPath!)
            .WithArguments(["--filter", $"*{ClassName}*"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task NextUnit()
    {
        await Cli.Wrap(_nextUnitPath!)
            .WithArguments(["--filter", $"*{ClassName}*"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task NUnit()
    {
        await Cli.Wrap(_nunitPath!)
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task MSTest()
    {
        await Cli.Wrap(_msTestPath!)
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task xUnit()
    {
        await Cli.Wrap(_xUnitPath!)
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }
}
