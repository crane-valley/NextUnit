using System.Buffers.Binary;
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
    public void PackageThatFailsWhileTheSignatureEntryIsReadFails()
    {
        string package = CreatePackageWithUnreadableSignatureEntry();
        try
        {
            // Pinned here rather than left to the fixture: this case only proves anything while the
            // archive still opens and still names the entry, because that is what puts the failure
            // past the open call and inside the part of the read the guard had to be widened for.
            using (ZipArchive archive = ZipFile.OpenRead(package))
            {
                Assert.Equal(
                    RepositorySignatureVerifier.SignatureEntryName,
                    Assert.Single(archive.Entries).FullName);
            }

            ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
                () => RepositorySignatureVerifier.Verify(package, NuGetServiceIndex));

            Assert.Contains("cannot be read as a zip archive", exception.Message);
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

    /// <summary>
    /// Builds a package that opens cleanly but whose signature entry cannot be read.
    /// </summary>
    /// <remarks>
    /// The central directory at the end of the file is left intact, so the archive opens and the
    /// entry is found, and only the entry payload is damaged: the failure lands on the read. A file
    /// that is simply not a zip fails inside the open instead, which the narrower guard this test
    /// exists for already covered.
    /// </remarks>
    private static string CreatePackageWithUnreadableSignatureEntry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nextunit-release-verify-{Guid.NewGuid():N}.nupkg");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using Stream signature = archive.CreateEntry(RepositorySignatureVerifier.SignatureEntryName).Open();
            signature.Write(new byte[256]);
        }

        byte[] bytes = File.ReadAllBytes(path);

        // The payload of the only entry follows its local header, whose fixed part is 30 bytes and
        // whose variable part is the two lengths that header carries. Reading them is what makes
        // the offset a fact about this archive rather than an assumption about the writer.
        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26));
        int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
        int payloadStart = 30 + nameLength + extraLength;

        // Mark the first deflate block with BTYPE 3, which the deflate specification reserves and
        // no decoder accepts. Flipping arbitrary payload bytes instead would leave the test resting
        // on what the compressor happened to emit for this input on this runtime.
        bytes[payloadStart] |= 0b0000_0110;
        File.WriteAllBytes(path, bytes);
        return path;
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
