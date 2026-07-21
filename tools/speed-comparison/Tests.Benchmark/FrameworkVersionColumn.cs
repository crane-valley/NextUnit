using System.Text.RegularExpressions;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Tests.Benchmark;

public class FrameworkVersionColumn : IColumn
{
    private static readonly Dictionary<string, string> _versionCache = new();

    public string Id => nameof(FrameworkVersionColumn);
    public string ColumnName => "Version";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Job;
    public int PriorityInCategory => 0;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Test Framework Version";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        return GetValue(summary, benchmarkCase, SummaryStyle.Default);
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var methodName = benchmarkCase.Descriptor.WorkloadMethod.Name;
        return GetFrameworkVersion(methodName);
    }

    public bool IsAvailable(Summary summary) => true;
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    private static string GetFrameworkVersion(string methodName)
    {
        if (_versionCache.TryGetValue(methodName, out var cachedVersion))
        {
            return cachedVersion;
        }

        var version = methodName switch
        {
            "NextUnitAsync" or "NextUnitAOTAsync" or "BuildNextUnitAsync" => GetNextUnitVersion(),
            "TUnitAsync" or "BuildTUnitAsync" => GetUnifiedProjectVersion("TUnitVersion"),
            "XUnitAsync" or "BuildXUnitAsync" => GetPackageVersion("xunit"),
            "NUnitAsync" or "BuildNUnitAsync" => GetPackageVersion("NUnit"),
            "MSTestAsync" or "BuildMSTestAsync" => GetPackageVersion("MSTest"),
            _ => "Unknown"
        };

        _versionCache[methodName] = version;
        return version;
    }

    private static string GetNextUnitVersion()
    {
        try
        {
            var repositoryRoot = FindRepositoryRoot();
            if (repositoryRoot is not null)
            {
                var buildPropsPath = Path.Combine(repositoryRoot.FullName, "Directory.Build.props");
                if (File.Exists(buildPropsPath))
                {
                    var content = File.ReadAllText(buildPropsPath);
                    var match = Regex.Match(content, @"<Version>([^<]+)</Version>");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return "";
        }
        catch (Exception)
        {
            // Version detection is not critical - return empty string on any error
            return "";
        }
    }

    private static string GetUnifiedProjectVersion(string propertyName)
    {
        try
        {
            var repositoryRoot = FindRepositoryRoot();
            if (repositoryRoot is not null)
            {
                var projectPath = Path.Combine(repositoryRoot.FullName, "tools", "speed-comparison", "UnifiedTests", "UnifiedTests.csproj");
                if (File.Exists(projectPath))
                {
                    var content = File.ReadAllText(projectPath);
                    var match = Regex.Match(content, $@"<{Regex.Escape(propertyName)}>([^<]+)</{Regex.Escape(propertyName)}>");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return "";
        }
        catch (Exception)
        {
            // Version detection is not critical - return empty string on any error
            return "";
        }
    }

    private static string GetPackageVersion(string packageName)
    {
        try
        {
            var repositoryRoot = FindRepositoryRoot();
            if (repositoryRoot is not null)
            {
                var packagesPropsPath = Path.Combine(repositoryRoot.FullName, "Directory.Packages.props");
                if (File.Exists(packagesPropsPath))
                {
                    var content = File.ReadAllText(packagesPropsPath);
                    var match = Regex.Match(content,
                        $@"<PackageVersion\s+Include=""{Regex.Escape(packageName)}""\s+Version=""([^""]+)""");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return "";
        }
        catch (Exception)
        {
            // Version detection is not critical - return empty string on any error
            return "";
        }
    }

    private static DirectoryInfo? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NextUnit.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
