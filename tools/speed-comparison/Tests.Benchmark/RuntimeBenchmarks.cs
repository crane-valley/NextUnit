using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using CliWrap;
using CliWrap.Buffered;

namespace Tests.Benchmark;

[BenchmarkCategory("Runtime")]
public class RuntimeBenchmarks : BenchmarkBase
{
    private static readonly string? ClassName = Environment.GetEnvironmentVariable("CLASS_NAME");

    [Benchmark]
    [BenchmarkCategory("Runtime", "AOT")]
    public async Task NextUnit_AOT()
    {
        var aotPath = Path.Combine(UnifiedPath, "bin", "Release-NEXTUNIT", Framework, "publish");
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UnifiedTests.exe" : "UnifiedTests";

        await Cli.Wrap(Path.Combine(aotPath, exeName))
            .WithArguments(["--filter", $"*{ClassName}*"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task NextUnit()
    {
        var binPath = Path.Combine(UnifiedPath, "bin", "Release-NEXTUNIT", Framework);
        var exeName = GetExecutableFileName();

        await Cli.Wrap(Path.Combine(binPath, exeName))
            .WithArguments(["--filter", $"*{ClassName}*"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task NUnit()
    {
        var binPath = Path.Combine(UnifiedPath, "bin", "Release-NUNIT", Framework);
        var exeName = GetExecutableFileName();

        await Cli.Wrap(Path.Combine(binPath, exeName))
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task MSTest()
    {
        var binPath = Path.Combine(UnifiedPath, "bin", "Release-MSTEST", Framework);
        var exeName = GetExecutableFileName();

        await Cli.Wrap(Path.Combine(binPath, exeName))
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }

    [Benchmark]
    public async Task xUnit()
    {
        var binPath = Path.Combine(UnifiedPath, "bin", "Release-XUNIT", Framework);
        var exeName = GetExecutableFileName();

        await Cli.Wrap(Path.Combine(binPath, exeName))
            .WithArguments(["--filter", $"FullyQualifiedName~{ClassName}"])
            .WithStandardOutputPipe(PipeTarget.ToStream(OutputStream))
            .ExecuteBufferedAsync();
    }
}
