using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyPairService : IKeyPairService
    {
        private readonly IKeyStore _keyStore;

        public KeyPairService(IKeyStore keyStore)
        {
            _keyStore = keyStore;
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

            // ✅ Safe to await now
            await _keyStore.SaveAsync(result.PublicKeyBase64, result.PrivateKeyBase64);

            return result;
        }

        public string SolveChallenge(string nonceBase64, string privateKeyBase64)
        {

            // --------------------------
            // 2️⃣ Decode inputs
            // --------------------------
            byte[] privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
            byte[] nonceBytes = Convert.FromBase64String(nonceBase64);

            // --------------------------
            // 3️⃣ Import private key (raw)
            // --------------------------
            var creationParams = new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            };

            using var key = Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);

            // --------------------------
            // 4️⃣ Sign the nonce
            // --------------------------
            byte[] signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, nonceBytes);

            if (signatureBytes.Length != 64)
            {
                Console.WriteLine($"ERROR: Signature length is {signatureBytes.Length}, expected 64 bytes!");
                return "";
            }

            string signatureBase64 = Convert.ToBase64String(signatureBytes);

            return signatureBase64;
        }

        public bool Verify(string publicKeyBase64, string signatureBase64, string nonceBase64)
        {
            try
            {
                byte[] publicKeyBytes;
                try { publicKeyBytes = Convert.FromBase64String(publicKeyBase64); }
                catch { Console.WriteLine("Verify: Invalid PublicKey Base64"); throw; }

                byte[] signatureBytes;
                try { signatureBytes = Convert.FromBase64String(signatureBase64); }
                catch { Console.WriteLine($"Verify: Invalid Signature Base64: '{signatureBase64}'"); throw; }

                byte[] nonceBytes;
                try { nonceBytes = Convert.FromBase64String(nonceBase64); }
                catch { Console.WriteLine("Verify: Invalid Nonce Base64"); throw; }

                var algorithm = SignatureAlgorithm.Ed25519;
                var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);

                return algorithm.Verify(publicKey, nonceBytes, signatureBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verify Exception: {ex.Message}");
                return false;
            }
        }
    }
}
