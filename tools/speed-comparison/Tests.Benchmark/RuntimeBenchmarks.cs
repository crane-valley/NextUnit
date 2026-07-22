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

    private string _nextUnitAotPath = "";
    private string _tUnitAotPath = "";
    private string? _nextUnitPath;
    private string? _tUnitPath;
    private string? _nunitPath;
    private string? _msTestPath;
    private string? _xUnitPath;

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

        var aotExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UnifiedTests.exe" : "UnifiedTests";
        var rid = GetRuntimeIdentifier();
        _nextUnitAotPath = GetAotExecutablePath("NEXTUNIT", rid, aotExeName);
        _tUnitAotPath = GetAotExecutablePath("TUNIT", rid, aotExeName);

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

        var autoBuildAot = string.Equals(
            Environment.GetEnvironmentVariable("AUTOBUILD_AOT"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        foreach (var (framework, displayName, executablePath) in new[]
        {
            ("NEXTUNIT", "NextUnit (AOT)", _nextUnitAotPath),
            ("TUNIT", "TUnit (AOT)", _tUnitAotPath)
        })
        {
            if (!File.Exists(executablePath) && autoBuildAot)
            {
                await TryBuildAotAsync(framework, executablePath, rid);
            }

            if (File.Exists(executablePath))
            {
                await VerifyTestCountAsync(displayName, executablePath);
                continue;
            }

            Console.WriteLine($"{displayName} executable not found at {executablePath}.");
            Console.WriteLine("Set AUTOBUILD_AOT=true or publish it with -p:Aot=true to include this benchmark.");
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

    private static string GetAotExecutablePath(string framework, string rid, string exeName)
    {
        return Path.Combine(UnifiedPath, "bin", $"Release-{framework}", Framework, rid, "publish", exeName);
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

    private static async Task TryBuildAotAsync(string framework, string executablePath, string rid)
    {
        Console.WriteLine($"Building {framework} Native AOT executable...");

        var exitCode = await RunDotNetAsync(
            "publish",
            "-c",
            "Release",
            $"-p:TestFramework={framework}",
            "-p:Aot=true",
            "-r",
            rid,
            "--framework",
            Framework,
            "--verbosity",
            "quiet");
        if (exitCode != 0)
        {
            Console.WriteLine($"Warning: {framework} AOT publish failed with exit code {exitCode}.");
        }
        else if (!File.Exists(executablePath))
        {
            Console.WriteLine($"Warning: {framework} AOT executable not found at {executablePath} after publish.");
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
        if (!File.Exists(_nextUnitAotPath))
        {
            throw new InvalidOperationException($"AOT executable not found at {_nextUnitAotPath}. Set AUTOBUILD_AOT=true or build manually to include this benchmark.");
        }

        await RunTestProcessAsync(_nextUnitAotPath);
    }

    [Benchmark]
    [BenchmarkCategory("Runtime", "AOT")]
    public async Task TUnitAOTAsync()
    {
        if (!File.Exists(_tUnitAotPath))
        {
            throw new InvalidOperationException($"AOT executable not found at {_tUnitAotPath}. Set AUTOBUILD_AOT=true or build manually to include this benchmark.");
        }

        await RunTestProcessAsync(_tUnitAotPath);
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
