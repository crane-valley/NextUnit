using System.Diagnostics.CodeAnalysis;

namespace NextUnit.ReleaseVerify;

/// <summary>
/// Entry point for the release-time structural check of a published package's repository signature.
/// </summary>
internal static class Program
{
    private const string VerifyCommand = "verify-repository-signature";
    private const string PackageOption = "--package";
    private const string ServiceIndexOption = "--expected-service-index";

    private const string Usage =
        $"usage: NextUnit.ReleaseVerify {VerifyCommand} {PackageOption} <path.nupkg> {ServiceIndexOption} <url>";

    // Exit 1 means the package failed the check; exit 2 means the tool was invoked wrong. Keeping the
    // two apart stops a broken workflow edit from reading, in the release log, as a bad package.
    private const int VerificationFailedExitCode = 1;
    private const int UsageErrorExitCode = 2;

    internal static int Main(string[] args)
    {
        if (!TryParseArguments(args, out string? package, out string? expectedServiceIndex, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return UsageErrorExitCode;
        }

        try
        {
            RepositorySignatureVerifier.Verify(package, expectedServiceIndex);
        }
        catch (ReleaseVerifyException ex)
        {
            Console.WriteLine($"::error::{EscapeAnnotationData(package)}: {EscapeAnnotationData(ex.Message)}");
            return VerificationFailedExitCode;
        }

        Console.WriteLine($"OK {package} repository-signed for {expectedServiceIndex}");
        return 0;
    }

    private static bool TryParseArguments(
        string[] args,
        [NotNullWhen(true)] out string? package,
        [NotNullWhen(true)] out string? expectedServiceIndex,
        [NotNullWhen(false)] out string? error)
    {
        package = null;
        expectedServiceIndex = null;
        error = null;

        if (args.Length == 0 || !string.Equals(args[0], VerifyCommand, StringComparison.Ordinal))
        {
            error = $"expected the '{VerifyCommand}' command";
            return false;
        }

        for (int index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
            {
                error = $"option '{args[index]}' has no value";
                return false;
            }

            // A repeated option is a usage error rather than last-one-wins. Silently taking the last
            // value would let a mangled workflow edit hand this tool a different file than the one
            // dotnet nuget verify judged, and the pair would still report a green package.
            switch (args[index])
            {
                case PackageOption:
                    if (package is not null)
                    {
                        error = $"option '{PackageOption}' is given more than once";
                        return false;
                    }

                    package = args[index + 1];
                    break;

                case ServiceIndexOption:
                    if (expectedServiceIndex is not null)
                    {
                        error = $"option '{ServiceIndexOption}' is given more than once";
                        return false;
                    }

                    expectedServiceIndex = args[index + 1];
                    break;

                default:
                    error = $"unknown option '{args[index]}'";
                    return false;
            }
        }

        if (string.IsNullOrEmpty(package))
        {
            error = $"{PackageOption} is required";
            return false;
        }

        if (string.IsNullOrEmpty(expectedServiceIndex))
        {
            error = $"{ServiceIndexOption} is required";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Escapes text for a GitHub Actions workflow command, the way GitHub documents.
    /// </summary>
    /// <remarks>
    /// A failure message quotes bytes decoded out of a downloaded package. An unescaped newline in
    /// there would end the annotation and let the remainder of the value start a workflow command of
    /// its own, so the value is escaped rather than trusted to be one line of text.
    /// </remarks>
    private static string EscapeAnnotationData(string value)
    {
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
    }
}
