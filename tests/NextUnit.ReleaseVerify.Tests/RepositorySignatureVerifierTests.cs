using System.IO.Compression;

namespace NextUnit.ReleaseVerify.Tests;

/// <summary>
/// Covers the structural rules the release workflow relies on to decide that a published package is
/// repository signed by nuget.org.
/// </summary>
public sealed class RepositorySignatureVerifierTests
{
    private const string NuGetServiceIndex = "https://api.nuget.org/v3/index.json";
    private const string OtherServiceIndex = "https://pkgs.example.invalid/v3/index.json";

    // A real ESS commitment type (proof-of-delivery) that carries no meaning for NuGet, standing in
    // for a value added to the specification after this tool was written.
    private const string UnknownCommitmentTypeOid = "1.2.840.113549.1.9.16.6.3";

    [Fact]
    public void PublishedSignatureNamesTheNuGetOrgServiceIndex()
    {
        RepositorySignatureVerifier.VerifySignatureBlob(ReadPublishedSignature(), NuGetServiceIndex);
    }

    [Fact]
    public void PublishedSignatureIsRejectedForAnotherServiceIndex()
    {
        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(ReadPublishedSignature(), OtherServiceIndex));

        Assert.Contains(NuGetServiceIndex, exception.Message);
        Assert.Contains(OtherServiceIndex, exception.Message);
    }

    [Fact]
    public void PackageCarryingThePublishedSignaturePasses()
    {
        string package = CreatePackage(ReadPublishedSignature());
        try
        {
            RepositorySignatureVerifier.Verify(package, NuGetServiceIndex);
        }
        finally
        {
            File.Delete(package);
        }
    }

    [Fact]
    public void PackageWithoutASignatureEntryFails()
    {
        string package = CreatePackage(signatureBlob: null);
        try
        {
            ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
                () => RepositorySignatureVerifier.Verify(package, NuGetServiceIndex));

            Assert.Contains(RepositorySignatureVerifier.SignatureEntryName, exception.Message);
        }
        finally
        {
            File.Delete(package);
        }
    }

    [Fact]
    public void RepositorySignerWithoutTheServiceIndexAttributeFails()
    {
        byte[] blob = SignatureBlobBuilder.Build(SignatureBlobBuilder.Repository());

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("no nuget-v3-service-index-url attribute", exception.Message);
    }

    [Fact]
    public void RepositorySignerWithTwoServiceIndexValuesFails()
    {
        byte[] blob = SignatureBlobBuilder.Build(
            SignatureBlobBuilder.Repository(NuGetServiceIndex, NuGetServiceIndex));

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("2 values, not exactly one", exception.Message);
    }

    [Fact]
    public void SignerWithoutACommitmentTypeIsUnknown()
    {
        byte[] blob = SignatureBlobBuilder.Build(new SignerSpec([], [NuGetServiceIndex]));

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("unknown signature type", exception.Message);
    }

    [Fact]
    public void SignerClaimingBothCommitmentTypesFails()
    {
        byte[] blob = SignatureBlobBuilder.Build(new SignerSpec(
            [SignatureBlobBuilder.ProofOfOriginOid, SignatureBlobBuilder.ProofOfReceiptOid],
            [NuGetServiceIndex]));

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("both proof-of-origin and proof-of-receipt", exception.Message);
    }

    [Fact]
    public void UnknownCommitmentTypeAlongsideProofOfReceiptStaysRepository()
    {
        byte[] blob = SignatureBlobBuilder.Build(new SignerSpec(
            [SignatureBlobBuilder.ProofOfReceiptOid, UnknownCommitmentTypeOid],
            [NuGetServiceIndex]));

        RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex);
    }

    [Fact]
    public void AuthorSignerWithOneRepositoryCountersignaturePasses()
    {
        byte[] blob = SignatureBlobBuilder.Build(
            SignatureBlobBuilder.Author(),
            SignatureBlobBuilder.Repository(NuGetServiceIndex));

        RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex);
    }

    [Fact]
    public void ServiceIndexOnTheAuthorSignerAloneDoesNotCount()
    {
        byte[] blob = SignatureBlobBuilder.Build(
            SignatureBlobBuilder.Author(NuGetServiceIndex),
            SignatureBlobBuilder.Repository());

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("no nuget-v3-service-index-url attribute", exception.Message);
    }

    [Fact]
    public void AuthorSignerWithTwoRepositoryCountersignaturesFails()
    {
        byte[] blob = SignatureBlobBuilder.Build(
            SignatureBlobBuilder.Author(),
            SignatureBlobBuilder.Repository(NuGetServiceIndex),
            SignatureBlobBuilder.Repository(NuGetServiceIndex));

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.VerifySignatureBlob(blob, NuGetServiceIndex));

        Assert.Contains("2 repository countersignatures", exception.Message);
    }

    private static byte[] ReadPublishedSignature()
    {
        return File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "nextunit.3.0.0.signature.p7s"));
    }

    private static string CreatePackage(byte[]? signatureBlob)
    {
        string path = Path.Combine(Path.GetTempPath(), $"nextunit-release-verify-{Guid.NewGuid():N}.nupkg");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (Stream nuspec = archive.CreateEntry("NextUnit.nuspec").Open())
            {
                nuspec.Write("<package />"u8);
            }

            if (signatureBlob is not null)
            {
                using Stream signature = archive.CreateEntry(RepositorySignatureVerifier.SignatureEntryName).Open();
                signature.Write(signatureBlob);
            }
        }

        return path;
    }
}
