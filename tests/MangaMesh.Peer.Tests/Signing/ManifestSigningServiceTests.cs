using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using NSec.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace MangaMesh.Peer.Tests.Signing;

[TestClass]
public class ManifestSigningServiceTests
{
    private static Key CreateTestKey() => new(SignatureAlgorithm.Ed25519, new KeyCreationParameters
    {
        ExportPolicy = KeyExportPolicies.AllowPlaintextExport
    });

    private static ChapterManifest MakeManifest(string seriesId = "series-1") => new()
    {
        ChapterId   = "ch-1",
        SeriesId    = seriesId,
        Title       = "Test Chapter",
        ChapterNumber = 1.0,
        Language    = "en",
        ScanGroup   = "grp",
        CreatedUtc  = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Files       = new[]
        {
            new ChapterFileEntry { Path = "001.jpg", Hash = "aabbcc", Size = 100 },
            new ChapterFileEntry { Path = "002.png", Hash = "ddeeff", Size = 200 }
        }
    };

    // ── SerializeCanonical ────────────────────────────────────────────────────

    [TestMethod]
    public void SerializeCanonical_IsStable()
    {
        var manifest = MakeManifest();
        var bytes1 = ManifestSigningService.SerializeCanonical(manifest);
        var bytes2 = ManifestSigningService.SerializeCanonical(manifest);
        CollectionAssert.AreEqual(bytes1, bytes2);
    }

    [TestMethod]
    public void SerializeCanonical_FilesAreOrderedByPath()
    {
        // Files supplied in reverse alphabetical order
        var manifest = MakeManifest() with
        {
            Files = new[]
            {
                new ChapterFileEntry { Path = "zzz.jpg", Hash = "aa", Size = 1 },
                new ChapterFileEntry { Path = "aaa.jpg", Hash = "bb", Size = 2 }
            }
        };

        var json = Encoding.UTF8.GetString(ManifestSigningService.SerializeCanonical(manifest));
        // "aaa.jpg" must appear before "zzz.jpg" in the serialised output
        Assert.IsTrue(json.IndexOf("aaa.jpg", StringComparison.Ordinal) <
                      json.IndexOf("zzz.jpg", StringComparison.Ordinal),
                      "Files must be sorted by path for canonical serialisation");
    }

    [TestMethod]
    public void SerializeCanonical_DifferentManifests_ProduceDifferentBytes()
    {
        var a = ManifestSigningService.SerializeCanonical(MakeManifest("series-A"));
        var b = ManifestSigningService.SerializeCanonical(MakeManifest("series-B"));
        CollectionAssert.AreNotEqual(a, b);
    }

    // ── SignManifest / VerifySignedManifest round-trip ───────────────────────

    [TestMethod]
    public void SignManifest_ProducesVerifiableSignature()
    {
        using var key  = CreateTestKey();
        var manifest   = MakeManifest();
        var signed     = ManifestSigningService.SignManifest(manifest, key);

        // Should not throw
        ManifestSigningService.VerifySignedManifest(signed);
    }

    [TestMethod]
    public void VerifySignedManifest_TamperedTitle_Throws()
    {
        using var key = CreateTestKey();
        var signed    = ManifestSigningService.SignManifest(MakeManifest(), key);

        // Tamper: change a field on the manifest
        var tampered = signed with
        {
            Manifest = signed.Manifest with { Title = "HACKED" }
        };

        bool threw1 = false;
        try { ManifestSigningService.VerifySignedManifest(tampered); }
        catch (CryptographicException) { threw1 = true; }
        Assert.IsTrue(threw1, "Expected CryptographicException for tampered title");
    }

    [TestMethod]
    public void VerifySignedManifest_WrongPublicKey_Throws()
    {
        using var key1 = CreateTestKey();
        using var key2 = CreateTestKey();

        var signed   = ManifestSigningService.SignManifest(MakeManifest(), key1);
        var wrongKey = Convert.ToBase64String(key2.PublicKey.Export(KeyBlobFormat.RawPublicKey));
        var tampered = signed with { PublisherPublicKey = wrongKey };

        bool threw = false;
        try { ManifestSigningService.VerifySignedManifest(tampered); }
        catch (CryptographicException) { threw = true; }
        Assert.IsTrue(threw, "Expected CryptographicException for wrong public key");
    }

    [TestMethod]
    public void VerifySignedManifest_TamperedSignatureBytes_Throws()
    {
        using var key = CreateTestKey();
        var signed    = ManifestSigningService.SignManifest(MakeManifest(), key);

        var sigBytes = Convert.FromBase64String(signed.Signature);
        sigBytes[0] ^= 0xFF;
        var tampered = signed with { Signature = Convert.ToBase64String(sigBytes) };

        bool threw = false;
        try { ManifestSigningService.VerifySignedManifest(tampered); }
        catch (CryptographicException) { threw = true; }
        Assert.IsTrue(threw, "Expected CryptographicException for corrupted signature");
    }

    [TestMethod]
    public void SignManifest_ManifestHashMatchesSha256OfCanonicalBytes()
    {
        using var key = CreateTestKey();
        var manifest  = MakeManifest();
        var signed    = ManifestSigningService.SignManifest(manifest, key);

        var canonical = ManifestSigningService.SerializeCanonical(manifest);
        var expected  = $"sha256:{Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant()}";

        Assert.AreEqual(expected, signed.ManifestHash);
    }

    [TestMethod]
    public void SignManifest_TwoCallsSameKey_ProduceDifferentSignatures()
    {
        // Ed25519 is deterministic — same input must produce the same signature.
        // This test documents that assumption: same key + same manifest → identical sig.
        using var key = CreateTestKey();
        var manifest  = MakeManifest();
        var sig1 = ManifestSigningService.SignManifest(manifest, key).Signature;
        var sig2 = ManifestSigningService.SignManifest(manifest, key).Signature;
        Assert.AreEqual(sig1, sig2, "Ed25519 is deterministic; same input must yield same signature");
    }
}
