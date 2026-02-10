using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace MangaMesh.Peer.ClientApi.Services
{
    public interface IChallengeService
    {
        (string Id, string Nonce) CreateChallenge(string publicKey);
        string? GetNonce(string challengeId);
        void Remove(string challengeId);
    }

    public class ChallengeService : IChallengeService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        public ChallengeService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public (string Id, string Nonce) CreateChallenge(string publicKey)
        {
            var id = Guid.NewGuid().ToString();
            var nonceBytes = new byte[32];
            RandomNumberGenerator.Fill(nonceBytes);
            var nonce = Convert.ToBase64String(nonceBytes);

            _cache.Set(id, nonce, _expiry);

            return (id, nonce);
        }

        public string? GetNonce(string challengeId)
        {
            _cache.TryGetValue(challengeId, out string? nonce);
            return nonce;
        }

        public void Remove(string challengeId)
        {
            _cache.Remove(challengeId);
        }
    }
}
