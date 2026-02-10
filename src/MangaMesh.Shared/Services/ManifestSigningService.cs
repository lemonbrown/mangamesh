using MangaMesh.Shared.Models;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Services
{
    public class ManifestSigningService
    {
        public static byte[] SerializeCanonical(ChapterManifest manifest)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Ensure deterministic ordering
            var normalized = manifest with
            {
                Files = manifest.Files
                    .OrderBy(f => f.Path, StringComparer.Ordinal)
                    .ToArray()
            };

            var json = JsonSerializer.Serialize(normalized, options);
            return Encoding.UTF8.GetBytes(json);
        }

        public static SignedChapterManifest SignManifest(
            ChapterManifest manifest,
            Key privateKey)
        {
            // 1. Canonical bytes
            byte[] manifestBytes = SerializeCanonical(manifest);

            // 2. Hash
            byte[] hash = SHA256.HashData(manifestBytes);

            // 3. Sign
            byte[] signature = SignatureAlgorithm.Ed25519.Sign(
                privateKey,
                hash);

            // 4. Export public key
            byte[] publicKey = privateKey.PublicKey.Export(
                KeyBlobFormat.RawPublicKey);

            return new SignedChapterManifest
            {
                Manifest = manifest,
                ManifestHash = $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}",
                Signature = Convert.ToBase64String(signature),
                PublisherPublicKey = Convert.ToBase64String(publicKey)
            };
        }

        public static void VerifySignedManifest(SignedChapterManifest signed)
        {
            // 1. Canonical bytes
            byte[] manifestBytes = SerializeCanonical(signed.Manifest);

            // 2. Hash
            byte[] computedHash = SHA256.HashData(manifestBytes);
            string computedHashHex =
                $"sha256:{Convert.ToHexString(computedHash).ToLowerInvariant()}";

            if (computedHashHex != signed.ManifestHash)
                throw new CryptographicException("Manifest hash mismatch");

            // 3. Import public key
            var publicKeyBytes = Convert.FromBase64String(signed.PublisherPublicKey);
            var publicKey = PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                publicKeyBytes,
                KeyBlobFormat.RawPublicKey);

            // 4. Verify signature
            var signatureBytes = Convert.FromBase64String(signed.Signature);

            bool valid = SignatureAlgorithm.Ed25519.Verify(
                publicKey,
                computedHash,
                signatureBytes);

            if (!valid)
            {
                var json = Encoding.UTF8.GetString(manifestBytes);
                throw new CryptographicException($"Invalid manifest signature. Computed Hash: {computedHashHex}, Expected Hash: {signed.ManifestHash}. Sig: {signed.Signature}, PubKey: {signed.PublisherPublicKey}. JSON: {json}");
            }
        }
    }
}
