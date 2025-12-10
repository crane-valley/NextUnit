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
    private bool _aotBuildAttempted;

    [GlobalSetup]
    public async Task Setup()
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
        await BuildIfMissingAsync("NEXTUNIT", _nextUnitPath);
        await BuildIfMissingAsync("NUNIT", _nunitPath);
        await BuildIfMissingAsync("MSTEST", _msTestPath);
        await BuildIfMissingAsync("XUNIT", _xUnitPath);

        // For AOT, only try to build it if AUTOBUILD_AOT environment variable is set
        // This is because AOT builds can take 5-10 minutes
        _aotBuildAttempted = false;
        if (!File.Exists(_aotPath))
        {
            var autoBuildAot = Environment.GetEnvironmentVariable("AUTOBUILD_AOT");
            if (!string.IsNullOrEmpty(autoBuildAot) && autoBuildAot.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                await TryBuildAotAsync();
            }
            else
            {
                Console.WriteLine($"AOT executable not found at {_aotPath}.");
                Console.WriteLine("To build it automatically, set environment variable AUTOBUILD_AOT=true");
                Console.WriteLine("Or run: dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true");
                Console.WriteLine("NextUnit_AOT benchmark will be skipped.");
            }
        }
    }

    private string GetExecutablePath(string framework, string exeName)
    {
        var binPath = Path.Combine(UnifiedPath, "bin", $"Release-{framework}", Framework);
        return Path.Combine(binPath, exeName);
    }

    private async Task BuildIfMissingAsync(string framework, string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            Console.WriteLine($"Building {framework} executable at {executablePath}...");
            var result = await Cli.Wrap("dotnet")
                .WithArguments(["build", "-c", "Release", "-p:TestFramework=" + framework, "--framework", Framework, "--verbosity", "quiet"])
                .WithWorkingDirectory(UnifiedPath)
                .ExecuteBufferedAsync();

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

    private async Task TryBuildAotAsync()
    {
        if (_aotBuildAttempted)
        {
            return;
        }

        _aotBuildAttempted = true;

        try
        {
            Console.WriteLine("AOT executable not found. Building AOT version (this may take several minutes)...");
            Console.WriteLine("To skip this, run: dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true");
            
            var result = await Cli.Wrap("dotnet")
                .WithArguments(["publish", "-c", "Release", "-p:TestFramework=NEXTUNIT", "-p:PublishAot=true", "--framework", Framework, "--verbosity", "quiet"])
                .WithWorkingDirectory(UnifiedPath)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                Console.WriteLine($"Warning: Failed to build AOT version: {result.StandardError}");
                Console.WriteLine("NextUnit_AOT benchmark will be skipped. Run the publish command manually to enable it.");
            }
            else if (!File.Exists(_aotPath))
            {
                Console.WriteLine($"Warning: AOT executable not found at {_aotPath} after publish.");
                Console.WriteLine("NextUnit_AOT benchmark will be skipped.");
            }
            else
            {
                Console.WriteLine("AOT build completed successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Exception while building AOT version: {ex.Message}");
            Console.WriteLine("NextUnit_AOT benchmark will be skipped.");
        }
    }

    [Benchmark]
    [BenchmarkCategory("Runtime", "AOT")]
    public async Task NextUnit_AOT()
    {
        if (!File.Exists(_aotPath))
        {
            // Skip this benchmark if AOT executable doesn't exist
            // We return immediately to avoid measuring a failed run
            throw new InvalidOperationException($"AOT executable not found at {_aotPath}. Run 'dotnet publish -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true' first, or let the automatic build complete in GlobalSetup.");
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
