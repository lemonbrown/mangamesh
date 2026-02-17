using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using NSec.Cryptography;
using System;
using System.Security.Cryptography;

namespace MangaMesh.Shared.Services
{

    public sealed class PublicKeyRegistry : IPublicKeyRegistry
    {
        private readonly IPublicKeyStore _keyStore;
        private readonly IChallengeStore _challengeStore;

        public PublicKeyRegistry(
            IPublicKeyStore keyStore,
            IChallengeStore challengeStore)
        {
            _keyStore = keyStore;
            _challengeStore = challengeStore;
        }

        public async Task<KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64)
        {
            Console.WriteLine($"=== Create Challenge ===");
            Console.WriteLine($"Public key received: {publicKeyBase64}");

            // 1. Generate secure random nonce (32 bytes is plenty)
            var nonceBytes = RandomNumberGenerator.GetBytes(32);
            var nonceBase64 = Convert.ToBase64String(nonceBytes);

            // 2. Create challenge
            var challenge = new KeyChallenge
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = publicKeyBase64,
                Nonce = nonceBase64,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            Console.WriteLine($"Challenge ID: {challenge.Id}");
            Console.WriteLine($"Stored as UserId: {challenge.UserId}");
            Console.WriteLine($"========================");

            // 3. Persist
            await _challengeStore.StoreAsync(challenge);

            // 4. Return response (no secrets)
            return new KeyChallengeResponse
            {
                ChallengeId = challenge.Id,
                Nonce = challenge.Nonce,
                ExpiresAt = challenge.ExpiresAt
            };
        }

        public async Task<KeyVerificationResponse> VerifyChallengeAsync(
            string challengeId,
            byte[] signature)
        {
            var challenge = await _challengeStore.GetAsync(challengeId);
            if (challenge == null || challenge.ExpiresAt < DateTimeOffset.UtcNow)
            {
                Console.WriteLine($"Challenge not found or expired: {challengeId}");
                return new KeyVerificationResponse { Valid = false };
            }

            Console.WriteLine($"Looking up key for UserId: {challenge.UserId}");

            var key = await _keyStore.GetByKeyAsync(challenge.UserId);
            if (key == null || key.Revoked)
            {
                Console.WriteLine($"Key not found or revoked: {challenge.UserId}");
                return new KeyVerificationResponse { Valid = false };
            }

            Console.WriteLine($"Key found! PublicKeyBase64: {key.PublicKeyBase64}");

            var nonceBytes = Convert.FromBase64String(challenge.Nonce);

            var publicKeyBytes = Convert.FromBase64String(key.PublicKeyBase64);

            Console.WriteLine($"=== Verification Debug ===");
            Console.WriteLine($"Challenge ID: {challengeId}");
            Console.WriteLine($"Nonce (base64): {challenge.Nonce}");
            Console.WriteLine($"Nonce (hex): {Convert.ToHexString(nonceBytes)}");
            Console.WriteLine($"Nonce length: {nonceBytes.Length} bytes");
            Console.WriteLine($"Public key (base64): {key.PublicKeyBase64}");
            Console.WriteLine($"Public key (hex): {Convert.ToHexString(publicKeyBytes)}");
            Console.WriteLine($"Public key length: {publicKeyBytes.Length} bytes");
            Console.WriteLine($"Signature (base64): {Convert.ToBase64String(signature)}");
            Console.WriteLine($"Signature (hex): {Convert.ToHexString(signature)}");
            Console.WriteLine($"Signature length: {signature.Length} bytes");

            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);

            var valid = algorithm.Verify(publicKey, nonceBytes, signature);

            Console.WriteLine($"Verification result: {valid}");
            Console.WriteLine($"========================");

            if (valid)
            {
                await _challengeStore.DeleteAsync(challengeId);
            }

            return new KeyVerificationResponse { Valid = valid };
        }
    }

}
