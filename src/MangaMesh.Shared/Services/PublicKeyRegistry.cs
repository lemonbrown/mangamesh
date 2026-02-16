using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
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
                return new KeyVerificationResponse { Valid = false };

            var key = await _keyStore.GetByKeyAsync(challenge.UserId);
            if (key == null || key.Revoked)
                return new KeyVerificationResponse { Valid = false };

            var nonceBytes = Convert.FromBase64String(challenge.Nonce);

            var publicKeyBytes = Convert.FromBase64String(key.PublicKeyBase64);

            var valid = Chaos.NaCl.Ed25519.Verify(
               new ArraySegment<byte>(signature),
               new ArraySegment<byte>(nonceBytes),
               new ArraySegment<byte>(publicKeyBytes)
           );

            if (valid)
            {
                await _challengeStore.DeleteAsync(challengeId);
            }

            return new KeyVerificationResponse { Valid = valid };
        }
    }

}
