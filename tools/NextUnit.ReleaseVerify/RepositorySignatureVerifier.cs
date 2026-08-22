using System.Formats.Asn1;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace NextUnit.ReleaseVerify;

/// <summary>
/// Structural reader for the CMS SignedData blob a NuGet package carries as <c>.signature.p7s</c>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here evaluates trust, certificate chains, timestamps, or content digests.
/// <c>dotnet nuget verify</c> is authoritative on all of that and runs first in the release
/// workflow, where its exit status is the validity verdict. This type answers only the two
/// questions that tool's human-readable output does not state on SDK 10: whether the package is
/// repository signed, and which service index the repository signature names.
/// </para>
/// <para>
/// The rules come from the NuGet package signature specification (Repository Signatures and
/// Countersignatures Technical Specification). No network access and no file writes are involved.
/// </para>
/// </remarks>
internal static class RepositorySignatureVerifier
{
    internal const string SignatureEntryName = ".signature.p7s";

    private const string CommitmentTypeIndicationOid = "1.2.840.113549.1.9.16.2.16";
    private const string ProofOfOriginOid = "1.2.840.113549.1.9.16.6.1";
    private const string ProofOfReceiptOid = "1.2.840.113549.1.9.16.6.2";
    private const string ServiceIndexUrlOid = "1.3.6.1.4.1.311.84.2.1.1.1";

    private enum SignatureKind
    {
        Unknown,
        Author,
        Repository,
    }

    /// <summary>
    /// Verifies that the package at <paramref name="packagePath"/> carries a repository signature
    /// naming <paramref name="expectedServiceIndex"/>.
    /// </summary>
    /// <exception cref="ReleaseVerifyException">The package fails the check.</exception>
    internal static void Verify(string packagePath, string expectedServiceIndex)
    {
        VerifySignatureBlob(ReadSignatureEntry(packagePath), expectedServiceIndex);
    }

    /// <summary>
    /// Verifies one already-extracted <c>.signature.p7s</c> blob.
    /// </summary>
    /// <exception cref="ReleaseVerifyException">The blob fails the check.</exception>
    internal static void VerifySignatureBlob(byte[] signatureBlob, string expectedServiceIndex)
    {
        SignedCms signedCms = new();
        try
        {
            signedCms.Decode(signatureBlob);
        }
        catch (CryptographicException ex)
        {
            throw new ReleaseVerifyException(
                $"has a {SignatureEntryName} that does not decode as a CMS SignedData blob ({ex.Message})",
                ex);
        }

        string observedServiceIndex = ReadServiceIndexUrl(SelectRepositorySigner(signedCms));
        if (!string.Equals(observedServiceIndex, expectedServiceIndex, StringComparison.Ordinal))
        {
            throw new ReleaseVerifyException(
                $"is repository signed for service index '{observedServiceIndex}', not '{expectedServiceIndex}'");
        }
    }

    private static byte[] ReadSignatureEntry(string packagePath)
    {
        // The whole read is guarded, not just the open: a zip reader decodes the central directory
        // lazily, so a truncated or corrupt archive throws while entries are enumerated or while the
        // entry stream is drained. Those escaping uncaught would exit with a runtime-chosen code and
        // break this tool's contract that a bad package is always exit 1.
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(packagePath);

            // A zip archive can hold several entries under one name and which of them a reader
            // returns is implementation defined, so an ambiguous archive is rejected rather than
            // resolved: the blob this tool reads has to be the only candidate in the file.
            List<ZipArchiveEntry> matches = [];
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, SignatureEntryName, StringComparison.Ordinal))
                {
                    matches.Add(entry);
                }
            }

            if (matches.Count == 0)
            {
                throw new ReleaseVerifyException(
                    $"contains no {SignatureEntryName} entry, so it carries no signature at all");
            }

            if (matches.Count > 1)
            {
                throw new ReleaseVerifyException(
                    $"contains {matches.Count} {SignatureEntryName} entries; a signed package has exactly 1");
            }

            using Stream entryStream = matches[0].Open();
            using MemoryStream buffer = new();
            entryStream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new ReleaseVerifyException($"cannot be read as a zip archive ({ex.Message})", ex);
        }
    }

    private static SignerInfo SelectRepositorySigner(SignedCms signedCms)
    {
        SignerInfoCollection signers = signedCms.SignerInfos;
        if (signers.Count != 1)
        {
            throw new ReleaseVerifyException(
                $"has {signers.Count} primary signers; a NuGet package signature has exactly 1");
        }

        SignerInfo primary = signers[0];
        switch (ClassifySigner(primary))
        {
            case SignatureKind.Repository:
                return primary;

            case SignatureKind.Author:
                return SelectRepositoryCountersigner(primary);

            default:
                throw new ReleaseVerifyException(
                    "has a primary signer of unknown signature type: its commitment-type-indication attribute "
                    + $"({CommitmentTypeIndicationOid}) names neither proof-of-origin nor proof-of-receipt");
        }
    }

    private static SignerInfo SelectRepositoryCountersigner(SignerInfo authorSigner)
    {
        // On an author-signed package nuget.org's repository signature is a countersignature of the
        // author signer. A countersigner of unknown type is skipped rather than rejected, because
        // only the repository ones are being counted; two repository countersigners is a structure
        // this tool has no rule for, so it fails instead of picking one.
        List<SignerInfo> repositoryCountersigners = [];
        foreach (SignerInfo counterSigner in authorSigner.CounterSignerInfos)
        {
            if (ClassifySigner(counterSigner) == SignatureKind.Repository)
            {
                repositoryCountersigners.Add(counterSigner);
            }
        }

        if (repositoryCountersigners.Count != 1)
        {
            throw new ReleaseVerifyException(
                $"is author signed and carries {repositoryCountersigners.Count} repository countersignatures; exactly 1 is required");
        }

        return repositoryCountersigners[0];
    }

    private static SignatureKind ClassifySigner(SignerInfo signer)
    {
        // NuGet's own aggregation rule (NuGet.Packaging AttributeUtility): collect the commitment
        // type ids across every instance and every value of the attribute, ignore ids this version
        // does not know so a value added to the specification later does not read as a failure, and
        // treat the two known ids appearing together as a contradiction rather than a precedence
        // question. Repeating one known id is not a contradiction.
        bool proofOfOrigin = false;
        bool proofOfReceipt = false;
        foreach (CryptographicAttributeObject attribute in signer.SignedAttributes)
        {
            if (!string.Equals(attribute.Oid.Value, CommitmentTypeIndicationOid, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (AsnEncodedData value in attribute.Values)
            {
                string commitmentTypeId = DecodeCommitmentTypeId(value.RawData);
                if (string.Equals(commitmentTypeId, ProofOfOriginOid, StringComparison.Ordinal))
                {
                    proofOfOrigin = true;
                }
                else if (string.Equals(commitmentTypeId, ProofOfReceiptOid, StringComparison.Ordinal))
                {
                    proofOfReceipt = true;
                }
            }
        }

        if (proofOfOrigin && proofOfReceipt)
        {
            throw new ReleaseVerifyException(
                "has a signer whose commitment-type-indication attribute claims both proof-of-origin and proof-of-receipt");
        }

        if (proofOfOrigin)
        {
            return SignatureKind.Author;
        }

        if (proofOfReceipt)
        {
            return SignatureKind.Repository;
        }

        return SignatureKind.Unknown;
    }

    private static string DecodeCommitmentTypeId(byte[] attributeValue)
    {
        AsnReader reader;
        string commitmentTypeId;
        try
        {
            reader = new AsnReader(attributeValue, AsnEncodingRules.DER);
            // CommitmentTypeIndication is a SEQUENCE of the id plus an OPTIONAL qualifier sequence,
            // so whatever follows the id inside that SEQUENCE is deliberately not inspected.
            commitmentTypeId = reader.ReadSequence().ReadObjectIdentifier();
        }
        catch (AsnContentException ex)
        {
            throw new ReleaseVerifyException(
                "has a commitment-type-indication value that is not a DER SEQUENCE opening with an object "
                + $"identifier ({ex.Message})",
                ex);
        }

        if (reader.HasData)
        {
            throw new ReleaseVerifyException(
                "has a commitment-type-indication value with trailing data after its SEQUENCE");
        }

        return commitmentTypeId;
    }

    private static string ReadServiceIndexUrl(SignerInfo repositorySigner)
    {
        // The specification requires exactly one instance of this attribute holding exactly one
        // value. CryptographicAttributeObjectCollection merges every instance of one OID into a
        // single attribute carrying all of their values, so through this API the two halves of that
        // rule are observable only as one total value count.
        List<AsnEncodedData> values = [];
        foreach (CryptographicAttributeObject attribute in repositorySigner.SignedAttributes)
        {
            if (!string.Equals(attribute.Oid.Value, ServiceIndexUrlOid, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (AsnEncodedData value in attribute.Values)
            {
                values.Add(value);
            }
        }

        if (values.Count == 0)
        {
            throw new ReleaseVerifyException(
                $"has a repository signer carrying no nuget-v3-service-index-url attribute ({ServiceIndexUrlOid})");
        }

        if (values.Count > 1)
        {
            throw new ReleaseVerifyException(
                "has a repository signer whose nuget-v3-service-index-url attribute carries "
                + $"{values.Count} values, not exactly one");
        }

        return DecodeServiceIndexUrl(values[0].RawData);
    }

    /// <summary>
    /// Decodes one nuget-v3-service-index-url attribute value.
    /// </summary>
    /// <exception cref="ReleaseVerifyException">
    /// The value is not exactly one DER IA5String and nothing else.
    /// </exception>
    internal static string DecodeServiceIndexUrl(byte[] attributeValue)
    {
        AsnReader reader;
        string serviceIndexUrl;
        try
        {
            reader = new AsnReader(attributeValue, AsnEncodingRules.DER);
            // IA5String is a 7-bit alphabet, so the reader rejects every byte an equality test
            // against the expected URL could not have matched anyway; that is what makes the
            // caller's ordinal comparison the byte-for-byte comparison the specification asks for.
            serviceIndexUrl = reader.ReadCharacterString(UniversalTagNumber.IA5String);
        }
        catch (AsnContentException ex)
        {
            throw new ReleaseVerifyException(
                $"has a nuget-v3-service-index-url value that is not a DER IA5String ({ex.Message})",
                ex);
        }

        if (reader.HasData)
        {
            // AsnReader.ThrowIfNotEmpty reports the same fact, but in a message that does not name
            // the attribute that drifted, and this text is read straight out of a release log.
            throw new ReleaseVerifyException(
                "has a nuget-v3-service-index-url value with trailing data after its IA5String");
        }

        return serviceIndexUrl;
    }
}
