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
            "NextUnit" or "NextUnit_AOT" or "Build_NextUnit" => GetNextUnitVersion(),
            "xUnit" or "Build_xUnit" => GetXUnitVersion(),
            "NUnit" or "Build_NUnit" => GetNUnitVersion(),
            "MSTest" or "Build_MSTest" => GetMSTestVersion(),
            _ => "Unknown"
        };

        _versionCache[methodName] = version;
        return version;
    }

    private static string GetNextUnitVersion()
    {
        try
        {
            // Try to get the version from the current codebase's Directory.Packages.props
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null && directory.Name != "NextUnit")
            {
                directory = directory.Parent;
            }

            if (directory != null)
            {
                var packagesPropsPath = Path.Combine(directory.FullName, "Directory.Packages.props");
                if (File.Exists(packagesPropsPath))
                {
                    var content = File.ReadAllText(packagesPropsPath);
                    var match = Regex.Match(content,
                        @"<PackageVersion\s+Include=""NextUnit""\s+Version=""([^""]+)""");
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

    private static string GetXUnitVersion()
    {
        try
        {
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null && directory.Name != "NextUnit")
            {
                directory = directory.Parent;
            }

            if (directory != null)
            {
                var packagesPropsPath = Path.Combine(directory.FullName, "Directory.Packages.props");
                if (File.Exists(packagesPropsPath))
                {
                    var content = File.ReadAllText(packagesPropsPath);
                    var match = Regex.Match(content,
                        @"<PackageVersion\s+Include=""xunit""\s+Version=""([^""]+)""");
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

    private static string GetNUnitVersion()
    {
        try
        {
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null && directory.Name != "NextUnit")
            {
                directory = directory.Parent;
            }

            if (directory != null)
            {
                var packagesPropsPath = Path.Combine(directory.FullName, "Directory.Packages.props");
                if (File.Exists(packagesPropsPath))
                {
                    var content = File.ReadAllText(packagesPropsPath);
                    var match = Regex.Match(content,
                        @"<PackageVersion\s+Include=""NUnit""\s+Version=""([^""]+)""");
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

    private static string GetMSTestVersion()
    {
        try
        {
            var directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null && directory.Name != "NextUnit")
            {
                directory = directory.Parent;
            }

            if (directory != null)
            {
                var packagesPropsPath = Path.Combine(directory.FullName, "Directory.Packages.props");
                if (File.Exists(packagesPropsPath))
                {
                    var content = File.ReadAllText(packagesPropsPath);
                    var match = Regex.Match(content,
                        @"<PackageVersion\s+Include=""MSTest""\s+Version=""([^""]+)""");
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
}
