using System.Text.RegularExpressions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Services;

namespace NextUnit.Platform;

/// <summary>
/// Builds a <see cref="TestFilterConfiguration"/> from command-line options and environment
/// variables.
/// </summary>
/// <remarks>
/// Command-line options win over environment variables for every filter: the environment is the
/// ambient default a CI job sets once, and an explicit argument is the caller overriding it for a
/// single run.
/// </remarks>
internal static class TestFilterConfigurationLoader
{
    public static TestFilterConfiguration Load(IServiceProvider services)
    {
        var config = new TestFilterConfiguration();
        var commandLineOptions = services.GetService<ICommandLineOptions>();

        var includeCategories = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.CategoryOption,
            "NEXTUNIT_INCLUDE_CATEGORIES");
        if (includeCategories.Count > 0)
        {
            config.IncludeCategories = includeCategories;
        }

        var excludeCategories = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.ExcludeCategoryOption,
            "NEXTUNIT_EXCLUDE_CATEGORIES");
        if (excludeCategories.Count > 0)
        {
            config.ExcludeCategories = excludeCategories;
        }

        var includeTags = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TagOption,
            "NEXTUNIT_INCLUDE_TAGS");
        if (includeTags.Count > 0)
        {
            config.IncludeTags = includeTags;
        }

        var excludeTags = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.ExcludeTagOption,
            "NEXTUNIT_EXCLUDE_TAGS");
        if (excludeTags.Count > 0)
        {
            config.ExcludeTags = excludeTags;
        }

        // Load test name patterns (wildcard support)
        var testNamePatterns = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TestNameOption,
            "NEXTUNIT_TEST_NAME");
        if (testNamePatterns.Count > 0)
        {
            config.TestNamePatterns = testNamePatterns;
        }

        var testNameRegexPatterns = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TestNameRegexOption,
            "NEXTUNIT_TEST_NAME_REGEX");
        if (testNameRegexPatterns.Count > 0)
        {
            config.TestNameRegexPatterns = CompileRegexPatterns(testNameRegexPatterns);
        }

        config.IncludeExplicitTests = LoadIncludeExplicitTests(commandLineOptions);

        return config;
    }

    private static List<Regex> CompileRegexPatterns(IReadOnlyList<string> patterns)
    {
        var regexList = new List<Regex>();

        foreach (var pattern in patterns)
        {
            try
            {
                regexList.Add(new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }
            catch (ArgumentException ex)
            {
                // Surface invalid patterns explicitly: silently dropping the only include filter
                // leaves RequiresDynamicExpansion false, so every test would run unfiltered.
                throw new ArgumentException(
                    $"Invalid --test-name-regex / NEXTUNIT_TEST_NAME_REGEX pattern '{pattern}': {ex.Message}",
                    ex);
            }
        }

        return regexList;
    }

    private static bool LoadIncludeExplicitTests(ICommandLineOptions? commandLineOptions)
    {
        var cliOptionSet = commandLineOptions is not null &&
            commandLineOptions.IsOptionSet(NextUnitCommandLineOptionsProvider.ExplicitOption);

        var explicitEnv = Environment.GetEnvironmentVariable("NEXTUNIT_INCLUDE_EXPLICIT");
        var envVarSet = string.Equals(explicitEnv, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitEnv, "1", StringComparison.OrdinalIgnoreCase);

        return cliOptionSet || envVarSet;
    }

    private static IReadOnlyList<string> GetFilterValues(
        ICommandLineOptions? commandLineOptions,
        string optionName,
        string environmentVariableName)
    {
        // Try CLI arguments first (higher priority)
        if (commandLineOptions is not null
            && commandLineOptions.IsOptionSet(optionName)
            && commandLineOptions.TryGetOptionArgumentList(optionName, out var arguments)
            && arguments is not null)
        {
            return arguments.ToList();
        }

        // Fall back to environment variable
        var envValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return Array.Empty<string>();
    }
}
