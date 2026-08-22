using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace NextUnit.ReleaseVerify.Tests;

/// <summary>
/// Describes the signed attributes one synthesized signer carries.
/// </summary>
internal sealed record SignerSpec(IReadOnlyList<string> CommitmentTypeIds, IReadOnlyList<string> ServiceIndexUrls);

/// <summary>
/// Builds CMS SignedData blobs shaped like a NuGet package signature.
/// </summary>
/// <remarks>
/// The published fixture shows one real shape only: a repository-signed package with a single
/// service index value. Every other shape the verifier has a rule for -- author signature plus
/// repository countersignature, a missing or repeated attribute, a contradictory commitment type --
/// has to be synthesized, because nuget.org will not produce it on request.
/// </remarks>
internal static class SignatureBlobBuilder
{
    internal const string CommitmentTypeIndicationOid = "1.2.840.113549.1.9.16.2.16";
    internal const string ProofOfOriginOid = "1.2.840.113549.1.9.16.6.1";
    internal const string ProofOfReceiptOid = "1.2.840.113549.1.9.16.6.2";
    internal const string ServiceIndexUrlOid = "1.3.6.1.4.1.311.84.2.1.1.1";

    private const string ExportPassword = "nextunit-release-verify-tests";

    internal static SignerSpec Repository(params string[] serviceIndexUrls)
    {
        return new SignerSpec([ProofOfReceiptOid], serviceIndexUrls);
    }

    internal static SignerSpec Author(params string[] serviceIndexUrls)
    {
        return new SignerSpec([ProofOfOriginOid], serviceIndexUrls);
    }

    /// <summary>
    /// Builds a signature whose primary signer is <paramref name="primary"/>, countersigned by
    /// <paramref name="counterSigners"/> in order.
    /// </summary>
    internal static byte[] Build(SignerSpec primary, params SignerSpec[] counterSigners)
    {
        using X509Certificate2 certificate = CreateSigningCertificate();

        // The content is never read: the verifier decodes structure and never checks a digest, and
        // dotnet nuget verify -- which does -- runs against the real package, not against this.
        SignedCms signedCms = new(new ContentInfo([0x4e, 0x65, 0x78, 0x74]));
        signedCms.ComputeSignature(CreateSigner(certificate, primary));

        foreach (SignerSpec counterSigner in counterSigners)
        {
            signedCms.SignerInfos[0].ComputeCounterSignature(CreateSigner(certificate, counterSigner));
        }

        return signedCms.Encode();
    }

    internal static byte[] EncodeCommitmentTypeIndication(string commitmentTypeId)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(commitmentTypeId);
        }

        return writer.Encode();
    }

    internal static byte[] EncodeCharacterString(UniversalTagNumber tag, string value)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteCharacterString(tag, value);
        return writer.Encode();
    }

    private static CmsSigner CreateSigner(X509Certificate2 certificate, SignerSpec spec)
    {
        CmsSigner signer = new(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly,
        };

        foreach (string commitmentTypeId in spec.CommitmentTypeIds)
        {
            signer.SignedAttributes.Add(new AsnEncodedData(
                new Oid(CommitmentTypeIndicationOid),
                EncodeCommitmentTypeIndication(commitmentTypeId)));
        }

        // Adding this OID twice yields one attribute carrying two values, which is the only way the
        // "exactly one value" rule can be violated through CryptographicAttributeObjectCollection.
        foreach (string serviceIndexUrl in spec.ServiceIndexUrls)
        {
            signer.SignedAttributes.Add(new AsnEncodedData(
                new Oid(ServiceIndexUrlOid),
                EncodeCharacterString(UniversalTagNumber.IA5String, serviceIndexUrl)));
        }

        return signer;
    }

    private static X509Certificate2 CreateSigningCertificate()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=NextUnit ReleaseVerify Tests",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using X509Certificate2 ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1));

        // CMS signing needs the private key reachable through a key container, which the handle
        // CreateSelfSigned returns does not have on Windows; a PKCS#12 round trip is what associates
        // one. Without PersistKeySet that container is removed again when the handle is disposed.
        byte[] pkcs12 = ephemeral.Export(X509ContentType.Pkcs12, ExportPassword);
        return X509CertificateLoader.LoadPkcs12(pkcs12, ExportPassword);
    }
}
