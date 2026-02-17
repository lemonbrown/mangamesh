using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using System.Security.Cryptography;

namespace MangaMesh.Shared.Services
{
    public sealed class PublicKeyRegistry : IPublicKeyRegistry
    {
        private readonly IPublicKeyStore _keyStore;
        private readonly IChallengeStore _challengeStore;
        private readonly ILogger<PublicKeyRegistry> _logger;

        public PublicKeyRegistry(
            IPublicKeyStore keyStore,
            IChallengeStore challengeStore,
            ILogger<PublicKeyRegistry> logger)
        {
            _keyStore = keyStore;
            _challengeStore = challengeStore;
            _logger = logger;
        }

        public async Task<KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64)
        {
            _logger.LogDebug("Creating challenge for public key: {PublicKey}", publicKeyBase64);

            var nonceBytes = RandomNumberGenerator.GetBytes(32);
            var nonceBase64 = Convert.ToBase64String(nonceBytes);

            var challenge = new KeyChallenge
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = publicKeyBase64,
                Nonce = nonceBase64,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            _logger.LogDebug("Challenge created: {ChallengeId}", challenge.Id);

            await _challengeStore.StoreAsync(challenge);

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
                _logger.LogWarning("Challenge not found or expired: {ChallengeId}", challengeId);
                return new KeyVerificationResponse { Valid = false };
            }

            _logger.LogDebug("Verifying challenge {ChallengeId} for key: {UserId}", challengeId, challenge.UserId);

            var key = await _keyStore.GetByKeyAsync(challenge.UserId);
            if (key == null || key.Revoked)
            {
                _logger.LogWarning("Key not found or revoked: {UserId}", challenge.UserId);
                return new KeyVerificationResponse { Valid = false };
            }

            var nonceBytes = Convert.FromBase64String(challenge.Nonce);
            var publicKeyBytes = Convert.FromBase64String(key.PublicKeyBase64);

            _logger.LogDebug("Verifying signature. Nonce: {NonceHex}, PublicKey: {PublicKeyHex}, Signature: {SigHex}",
                Convert.ToHexString(nonceBytes),
                Convert.ToHexString(publicKeyBytes),
                Convert.ToHexString(signature));

            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);

            var valid = algorithm.Verify(publicKey, nonceBytes, signature);

            _logger.LogDebug("Challenge {ChallengeId} verification result: {Valid}", challengeId, valid);

            if (valid)
            {
                await _challengeStore.DeleteAsync(challengeId);
            }

            return new KeyVerificationResponse { Valid = valid };
        }
    }
}
