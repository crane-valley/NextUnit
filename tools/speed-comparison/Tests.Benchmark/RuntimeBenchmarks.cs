using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Cysharp.Diagnostics;
using Microsoft.Diagnostics.Utilities;

namespace Tests.Benchmark;

[BenchmarkCategory("Runtime")]
public class RuntimeBenchmarks : BenchmarkBase
{
    private static readonly Regex ClassNameValidationRegex = new Regex(@"^[a-zA-Z0-9._]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> ValidRuntimeIdentifiers = new HashSet<string>
    {
        "win-x64", "win-x86", "win-arm64",
        "linux-x64", "linux-arm64", "linux-arm",
        "osx-x64", "osx-arm64"
    };

    private static readonly string? _className = SanitizeClassName(Environment.GetEnvironmentVariable("CLASS_NAME"));
    private string? _aotPath;
    private string? _nextUnitPath;
    private string? _nunitPath;
    private string? _msTestPath;
    private string? _xUnitPath;
    private bool _aotBuildAttempted;

    private static string? SanitizeClassName(string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        // Only allow alphanumeric characters, dots, and underscores
        // This prevents command injection while allowing typical class name patterns
        if (!ClassNameValidationRegex.IsMatch(className))
        {
            throw new ArgumentException($"Invalid CLASS_NAME value: '{className}'. Only alphanumeric characters, dots, and underscores are allowed.");
        }

        return className;
    }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        // Validate and cache executable paths
        var exeName = GetExecutableFileName();

        // Regular builds
        _nextUnitPath = GetExecutablePath("NEXTUNIT", exeName);
        _nunitPath = GetExecutablePath("NUNIT", exeName);
        _msTestPath = GetExecutablePath("MSTEST", exeName);
        _xUnitPath = GetExecutablePath("XUNIT", exeName);

        // AOT build (different path - in publish folder with RID)
        var aotExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UnifiedTests.exe" : "UnifiedTests";
        var rid = GetRuntimeIdentifier();
        var aotPath = Path.Combine(UnifiedPath, "bin", "Release-NEXTUNIT", Framework, rid, "publish");
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
        if (!ValidRuntimeIdentifiers.Contains(rid))
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

    private async Task BuildIfMissingAsync(string framework, string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            Console.WriteLine($"Building {framework} executable at {executablePath}...");

            var command = $"dotnet build -c Release -p:TestFramework={framework} --framework {Framework} --verbosity quiet";
            var (process, stdOut, stdErr) = ProcessX.GetDualAsyncEnumerable(command, workingDirectory: UnifiedPath);
            
            // Process stdout and stderr concurrently to avoid potential deadlocks
            var stdOutTask = Task.Run(async () =>
            {
                await foreach (var line in stdOut)
                {
                    Console.WriteLine(line);
                }
            });
            
            var stdErrTask = Task.Run(async () =>
            {
                await foreach (var line in stdErr)
                {
                    Console.Error.WriteLine(line);
                }
            });
            
            await Task.WhenAll(stdOutTask, stdErrTask);
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"{framework} build failed with exit code {exitCode}.");
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

        var rid = GetRuntimeIdentifier();
        Console.WriteLine("AOT executable not found. Building AOT version (this may take several minutes)...");
        Console.WriteLine($"To build manually instead, run: dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true -r {rid}");

        var command = $"dotnet publish -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true -r {rid} --framework {Framework} --verbosity quiet";
        var (process, stdOut, stdErr) = ProcessX.GetDualAsyncEnumerable(command, workingDirectory: UnifiedPath);
        
        // Process stdout and stderr concurrently to avoid potential deadlocks
        var stdOutTask = Task.Run(async () =>
        {
            await foreach (var line in stdOut)
            {
                Console.WriteLine(line);
            }
        });
        
        var stdErrTask = Task.Run(async () =>
        {
            await foreach (var line in stdErr)
            {
                Console.Error.WriteLine(line);
            }
        });
        
        await Task.WhenAll(stdOutTask, stdErrTask);
        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;

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
        await foreach (var output in ProcessX.StartAsync(_aotPath))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task NextUnitAsync()
    {
        // NextUnit uses Microsoft.Testing.Platform which doesn't support --filter in the same way as Microsoft.NET.Test.Sdk
        // Run all tests since filtering by class name is not directly supported
        await foreach (var output in ProcessX.StartAsync(_nextUnitPath!))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task NUnitAsync()
    {
        var command = _nunitPath!;

        // Only apply filter if CLASS_NAME environment variable is set
        if (!string.IsNullOrEmpty(_className))
        {
            command = command + $" --filter FullyQualifiedName~{_className}";
        }

        await foreach (var output in ProcessX.StartAsync(command))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task MSTestAsync()
    {
        var command = _msTestPath!;

        // Only apply filter if CLASS_NAME environment variable is set
        if (!string.IsNullOrEmpty(_className))
        {
            command = command + $" --filter FullyQualifiedName~{_className}";
        }

        await foreach (var output in ProcessX.StartAsync(command))
        {
            Console.WriteLine(output);
        }
    }

    [Benchmark]
    public async Task XUnitAsync()
    {
        var command = _xUnitPath!;

        // Only apply filter if CLASS_NAME environment variable is set
        if (!string.IsNullOrEmpty(_className))
        {
            command = command + $" --filter FullyQualifiedName~{_className}";
        }

        await foreach (var output in ProcessX.StartAsync(command))
        {
            Console.WriteLine(output);
        }
    }
}
