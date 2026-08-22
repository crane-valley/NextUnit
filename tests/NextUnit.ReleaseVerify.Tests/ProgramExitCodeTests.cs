using System.IO.Compression;

namespace NextUnit.ReleaseVerify.Tests;

/// <summary>
/// Pins the exit codes the release workflow reads: 1 is a package that failed the check, 2 is a bad
/// invocation.
/// </summary>
/// <remarks>
/// The workflow only needs non-zero to fail the release, so these cases matter for what an operator
/// concludes from a red log rather than for whether it goes red. A usage error reported as 1 would
/// read as a defective published package.
/// </remarks>
public sealed class ProgramExitCodeTests
{
    private const string NuGetServiceIndex = "https://api.nuget.org/v3/index.json";

    [Fact]
    public void RepeatedPackageOptionIsAUsageError()
    {
        Assert.Equal(
            2,
            Program.Main([
                "verify-repository-signature",
                "--package", "first.nupkg",
                "--package", "second.nupkg",
                "--expected-service-index", NuGetServiceIndex]));
    }

    [Fact]
    public void RepeatedServiceIndexOptionIsAUsageError()
    {
        Assert.Equal(
            2,
            Program.Main([
                "verify-repository-signature",
                "--package", "first.nupkg",
                "--expected-service-index", NuGetServiceIndex,
                "--expected-service-index", "https://pkgs.example.invalid/v3/index.json"]));
    }

    [Fact]
    public void UnknownOptionIsAUsageError()
    {
        Assert.Equal(
            2,
            Program.Main([
                "verify-repository-signature",
                "--package", "first.nupkg",
                "--expected-service-index", NuGetServiceIndex,
                "--force", "yes"]));
    }

    [Fact]
    public void MissingServiceIndexIsAUsageError()
    {
        Assert.Equal(2, Program.Main(["verify-repository-signature", "--package", "first.nupkg"]));
    }

    [Fact]
    public void AnUnsignedPackageIsAVerificationFailure()
    {
        string package = CreateUnsignedPackage();
        try
        {
            Assert.Equal(
                1,
                Program.Main([
                    "verify-repository-signature",
                    "--package", package,
                    "--expected-service-index", NuGetServiceIndex]));
        }
        finally
        {
            File.Delete(package);
        }
    }

    [Fact]
    public void AnUnreadablePackageIsAVerificationFailure()
    {
        string package = Path.Combine(Path.GetTempPath(), $"nextunit-release-verify-{Guid.NewGuid():N}.nupkg");
        File.WriteAllBytes(package, [0x4e, 0x6f, 0x74, 0x5a, 0x69, 0x70]);
        try
        {
            Assert.Equal(
                1,
                Program.Main([
                    "verify-repository-signature",
                    "--package", package,
                    "--expected-service-index", NuGetServiceIndex]));
        }
        finally
        {
            File.Delete(package);
        }
    }

    private static string CreateUnsignedPackage()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nextunit-release-verify-{Guid.NewGuid():N}.nupkg");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using Stream nuspec = archive.CreateEntry("NextUnit.nuspec").Open();
            nuspec.Write("<package />"u8);
        }

        return path;
    }
}
