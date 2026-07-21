using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Tests.Benchmark;

[BenchmarkCategory("Runtime")]
public class RuntimeBenchmarks : BenchmarkBase
{
    private static readonly HashSet<string> _validRuntimeIdentifiers = new HashSet<string>
    {
        "win-x64", "win-x86", "win-arm64",
        "linux-x64", "linux-arm64", "linux-arm",
        "osx-x64", "osx-arm64"
    };

    private string? _aotPath;
    private string? _nextUnitPath;
    private string? _tUnitPath;
    private string? _nunitPath;
    private string? _msTestPath;
    private string? _xUnitPath;
    private bool _aotBuildAttempted;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        // Validate and cache executable paths
        var exeName = GetExecutableFileName();

        // Regular builds
        _nextUnitPath = GetExecutablePath("NEXTUNIT", exeName);
        _tUnitPath = GetExecutablePath("TUNIT", exeName);
        _nunitPath = GetExecutablePath("NUNIT", exeName);
        _msTestPath = GetExecutablePath("MSTEST", exeName);
        _xUnitPath = GetExecutablePath("XUNIT", exeName);

        // AOT build (different path - in publish folder with RID)
        var aotExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UnifiedTests.exe" : "UnifiedTests";
        var rid = GetRuntimeIdentifier();
        var aotPath = Path.Combine(UnifiedPath, "bin", "Release-NEXTUNIT", Framework, rid, "publish");
        _aotPath = Path.Combine(aotPath, aotExeName);

        // Incremental builds prevent stale binaries from being measured after package or source changes.
        await BuildExecutableAsync("NEXTUNIT", _nextUnitPath);
        await BuildExecutableAsync("TUNIT", _tUnitPath);
        await BuildExecutableAsync("NUNIT", _nunitPath);
        await BuildExecutableAsync("MSTEST", _msTestPath);
        await BuildExecutableAsync("XUNIT", _xUnitPath);

        await VerifyTestCountAsync("NextUnit", _nextUnitPath);
        await VerifyTestCountAsync("TUnit", _tUnitPath);
        await VerifyTestCountAsync("NUnit", _nunitPath);
        await VerifyTestCountAsync("MSTest", _msTestPath);
        await VerifyTestCountAsync("xUnit", _xUnitPath);

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
                Console.WriteLine($"Or run: dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true -r {rid}");
                Console.WriteLine("NextUnit_AOT benchmark will be skipped.");
            }
        }
    }

    private string GetRuntimeIdentifier()
    {
        // Determine the runtime identifier based on the current platform
        string rid;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                Architecture.X86 => "win-x86",
                _ => throw new PlatformNotSupportedException($"Unsupported Windows architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                Architecture.Arm => "linux-arm",
                _ => throw new PlatformNotSupportedException($"Unsupported Linux architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => throw new PlatformNotSupportedException($"Unsupported macOS architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }
        else
        {
            throw new PlatformNotSupportedException($"Unsupported operating system: {RuntimeInformation.OSDescription}");
        }

        // Validate against whitelist as an extra safety measure
        if (!_validRuntimeIdentifiers.Contains(rid))
        {
            throw new PlatformNotSupportedException($"Runtime identifier '{rid}' is not in the validated whitelist.");
        }

        return rid;
    }

    private string GetExecutablePath(string framework, string exeName)
    {
        var binPath = Path.Combine(UnifiedPath, "bin", $"Release-{framework}", Framework);
        // Validate exeName for safety: only allow base file names with alphanumeric, dot, underscore, and dash
        if (exeName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1 || exeName.Contains(Path.DirectorySeparatorChar) || exeName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException($"Invalid exeName value: '{exeName}'. Only a simple filename is allowed.");
        }
        return Path.Combine(binPath, exeName);
    }

    private async Task BuildExecutableAsync(string framework, string executablePath)
    {
        Console.WriteLine($"Building {framework} executable at {executablePath}...");

        var exitCode = await RunDotNetAsync(
            "build",
            "-c",
            "Release",
            $"-p:TestFramework={framework}",
            "--framework",
            Framework,
            "--verbosity",
            "quiet");
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{framework} build failed with exit code {exitCode}.");
        }

        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException($"{framework} executable not found at {executablePath} after build.");
        }
    }

    private async Task TryBuildAotAsync()
    {
        if (_aotBuildAttempted)
        {
            return;
        }

        _aotBuildAttempted = true;

        var rid = GetRuntimeIdentifier();
        Console.WriteLine("AOT executable not found. Building AOT version (this may take several minutes)...");
        Console.WriteLine($"To build manually instead, run: dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true -r {rid}");

        var exitCode = await RunDotNetAsync(
            "publish",
            "-c",
            "Release",
            "-p:TestFramework=NEXTUNIT",
            "-p:PublishAot=true",
            "-r",
            rid,
            "--framework",
            Framework,
            "--verbosity",
            "quiet");
        if (exitCode != 0)
        {
            Console.WriteLine($"Warning: AOT publish failed with exit code {exitCode}.");
            Console.WriteLine("NextUnit_AOT benchmark will be skipped.");
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

    private static async Task<int> RunDotNetAsync(params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = UnifiedPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        Console.Write(await standardOutput);
        Console.Error.Write(await standardError);
        return process.ExitCode;
    }

    private static async Task VerifyTestCountAsync(string framework, string executablePath)
    {
        var result = await RunTestProcessAsync(executablePath, captureOutput: true);
        if (!result.Output.Contains("total: 127", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{framework} did not report the expected 127 tests. Output:{Environment.NewLine}{result.Output}");
        }
    }

    private static async Task<(int ExitCode, string Output)> RunTestProcessAsync(
        string executablePath,
        bool captureOutput = false)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--no-progress");
        startInfo.ArgumentList.Add("--no-ansi");
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["TESTINGPLATFORM_UI_LANGUAGE"] = "en-us";
        startInfo.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["TUNIT_DISABLE_HTML_REPORTER"] = "true";

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {executablePath}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var output = (await standardOutput) + (await standardError);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Test process exited with code {process.ExitCode}: {executablePath}{Environment.NewLine}{output}");
        }

        return (process.ExitCode, captureOutput ? output : string.Empty);
    }

    [Benchmark]
    [BenchmarkCategory("Runtime", "AOT")]
    public async Task NextUnitAOTAsync()
    {
        if (!File.Exists(_aotPath))
        {
            // AOT executable doesn't exist - throw to exclude this benchmark from results
            // This is better than returning early which would record an invalid (very fast) result
            throw new InvalidOperationException($"AOT executable not found at {_aotPath}. Set AUTOBUILD_AOT=true or build manually to include this benchmark.");
        }

        // NextUnit uses Microsoft.Testing.Platform which doesn't support --filter in the same way as Microsoft.NET.Test.Sdk
        // Run all tests since filtering by class name is not directly supported
        await RunTestProcessAsync(_aotPath);
    }

    [Benchmark(Baseline = true)]
    public async Task NextUnitAsync()
    {
        await RunTestProcessAsync(_nextUnitPath!);
    }

    [Benchmark]
    public async Task TUnitAsync()
    {
        await RunTestProcessAsync(_tUnitPath!);
    }

    [Benchmark]
    public async Task NUnitAsync()
    {
        await RunTestProcessAsync(_nunitPath!);
    }

    [Benchmark]
    public async Task MSTestAsync()
    {
        await RunTestProcessAsync(_msTestPath!);
    }

    [Benchmark]
    public async Task XUnitAsync()
    {
        await RunTestProcessAsync(_xUnitPath!);
    }
}
