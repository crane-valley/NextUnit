using System.Formats.Asn1;

namespace NextUnit.ReleaseVerify.Tests;

/// <summary>
/// Covers the single-value decoder directly, with hand-built attribute bytes.
/// </summary>
/// <remarks>
/// A malformed attribute value cannot be reached end to end: <see cref="System.Security.Cryptography.Pkcs.SignedCms"/>
/// rejects broken ASN.1 while signing, so a blob carrying one can never be synthesized here. Calling
/// the decoder directly is what keeps the trailing-data and wrong-tag rules under test at all.
/// </remarks>
public sealed class ServiceIndexUrlDecoderTests
{
    private const string NuGetServiceIndex = "https://api.nuget.org/v3/index.json";

    [Fact]
    public void ReadsAnIa5String()
    {
        byte[] value = SignatureBlobBuilder.EncodeCharacterString(UniversalTagNumber.IA5String, NuGetServiceIndex);

        Assert.Equal(NuGetServiceIndex, RepositorySignatureVerifier.DecodeServiceIndexUrl(value));
    }

    [Fact]
    public void RejectsTrailingDataAfterTheIa5String()
    {
        byte[] ia5String = SignatureBlobBuilder.EncodeCharacterString(UniversalTagNumber.IA5String, NuGetServiceIndex);
        byte[] derNull = [0x05, 0x00];
        byte[] value = [.. ia5String, .. derNull];

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.DecodeServiceIndexUrl(value));

        Assert.Contains("trailing data", exception.Message);
    }

    [Fact]
    public void RejectsANonIa5StringTag()
    {
        byte[] value = SignatureBlobBuilder.EncodeCharacterString(UniversalTagNumber.UTF8String, NuGetServiceIndex);

        ReleaseVerifyException exception = Assert.Throws<ReleaseVerifyException>(
            () => RepositorySignatureVerifier.DecodeServiceIndexUrl(value));

        Assert.Contains("not a DER IA5String", exception.Message);
    }
}
