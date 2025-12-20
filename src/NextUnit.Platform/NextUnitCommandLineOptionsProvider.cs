using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace NextUnit.Platform;

/// <summary>
/// Provides command-line option definitions for NextUnit test filtering.
/// </summary>
internal sealed class NextUnitCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string CategoryOption = "category";
    public const string ExcludeCategoryOption = "exclude-category";
    public const string TagOption = "tag";
    public const string ExcludeTagOption = "exclude-tag";
    public const string TestNameOption = "test-name";
    public const string TestNameRegexOption = "test-name-regex";

    /// <summary>
    /// Gets the unique identifier for this extension.
    /// </summary>
    public string Uid => nameof(NextUnitCommandLineOptionsProvider);

    /// <summary>
    /// Gets the version of this extension.
    /// </summary>
    public string Version => "1.6.2";

    /// <summary>
    /// Gets the display name of this extension.
    /// </summary>
    public string DisplayName => "NextUnit Filter Options";

    /// <summary>
    /// Gets the description of this extension.
    /// </summary>
    public string Description => "Command-line options for filtering tests by category, tag, and name";

    /// <summary>
    /// Determines whether the extension is enabled.
    /// </summary>
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <summary>
    /// Gets the command-line option definitions.
    /// </summary>
    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        return new[]
        {
            new CommandLineOption(
                CategoryOption,
                "Include only tests with the specified category (can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false),

            new CommandLineOption(
                ExcludeCategoryOption,
                "Exclude tests with the specified category (can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false),

            new CommandLineOption(
                TagOption,
                "Include only tests with the specified tag (can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false),

            new CommandLineOption(
                ExcludeTagOption,
                "Exclude tests with the specified tag (can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false),

            new CommandLineOption(
                TestNameOption,
                "Include only tests matching the specified name pattern (supports * and ? wildcards, can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false),

            new CommandLineOption(
                TestNameRegexOption,
                "Include only tests matching the specified regular expression pattern (can be specified multiple times)",
                ArgumentArity.OneOrMore,
                isHidden: false)
        };
    }

    /// <summary>
    /// Validates the option arguments.
    /// </summary>
    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        // All arguments are valid strings, no special validation needed
        return ValidationResult.ValidTask;
    }

    /// <summary>
    /// Validates all command-line options together.
    /// </summary>
    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        // No cross-option validation needed
        return ValidationResult.ValidTask;
    }
}
