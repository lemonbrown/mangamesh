using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyPairService : IKeyPairService
    {
        private readonly IKeyStore _keyStore;
        private readonly ILogger<KeyPairService> _logger;

        public KeyPairService(IKeyStore keyStore, ILogger<KeyPairService> logger)
        {
            _keyStore = keyStore;
            _logger = logger;
        }

        public KeyPairResult GenerateKeyPairBase64()
        {
            var creationParameters = new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            };

            using var key = new Key(SignatureAlgorithm.Ed25519, creationParameters);

            var privateKeyBase64 = Convert.ToBase64String(
                key.Export(KeyBlobFormat.RawPrivateKey));

            var publicKeyBase64 = Convert.ToBase64String(
                key.Export(KeyBlobFormat.RawPublicKey));

            return new KeyPairResult(privateKeyBase64, publicKeyBase64);
        }

        public async Task<KeyPairResult> GenerateKeyPairBase64Async()
        {
            var result = GenerateKeyPairBase64();
            await _keyStore.SaveAsync(result.PublicKeyBase64, result.PrivateKeyBase64);
            return result;
        }

        public string SolveChallenge(string nonceBase64, string privateKeyBase64)
        {
            byte[] privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
            byte[] nonceBytes = Convert.FromBase64String(nonceBase64);

            using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);

            var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);

            _logger.LogDebug("Signing challenge. Nonce: {NonceHex}, PublicKey: {PublicKeyHex}",
                Convert.ToHexString(nonceBytes),
                Convert.ToHexString(publicKeyBytes));

            byte[] signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, nonceBytes);

            if (signatureBytes.Length != 64)
            {
                _logger.LogError("Unexpected signature length: {Length} (expected 64)", signatureBytes.Length);
                return "";
            }

            _logger.LogDebug("Signature produced: {SignatureHex}", Convert.ToHexString(signatureBytes));

            return Convert.ToBase64String(signatureBytes);
        }

        public bool Verify(string publicKeyBase64, string signatureBase64, string nonceBase64)
        {
            try
            {
                byte[] publicKeyBytes;
                try { publicKeyBytes = Convert.FromBase64String(publicKeyBase64); }
                catch { _logger.LogWarning("Verify: Invalid PublicKey Base64"); throw; }

                byte[] signatureBytes;
                try { signatureBytes = Convert.FromBase64String(signatureBase64); }
                catch { _logger.LogWarning("Verify: Invalid Signature Base64: {Sig}", signatureBase64); throw; }

                byte[] nonceBytes;
                try { nonceBytes = Convert.FromBase64String(nonceBase64); }
                catch { _logger.LogWarning("Verify: Invalid Nonce Base64"); throw; }

                var algorithm = SignatureAlgorithm.Ed25519;
                var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);

                return algorithm.Verify(publicKey, nonceBytes, signatureBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Verify failed");
                return false;
            }
        }
    }
}
