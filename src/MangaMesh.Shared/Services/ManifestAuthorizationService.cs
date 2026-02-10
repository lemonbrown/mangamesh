using System.Collections.Concurrent;

namespace MangaMesh.Shared.Services
{
    public class ManifestAuthorizationService : IManifestAuthorizationService
    {
        // Key: NodeId:ManifestHash, Value: Expiration Time
        private readonly ConcurrentDictionary<string, DateTimeOffset> _authorizations = new();
        private readonly TimeSpan _validityPeriod = TimeSpan.FromMinutes(5);

        public void Authorize(string nodeId, string manifestHash)
        {
            var key = GetKey(nodeId, manifestHash);
            var expiresAt = DateTimeOffset.UtcNow.Add(_validityPeriod);
            _authorizations[key] = expiresAt;

            // Cleanup expired entries periodically? 
            // For now, let's just do a lazy cleanup on access or ignore memory growth (it's small strings).
            // But to be safe, let's remove expired if we stumble upon them?
            // Actually, ConcurrentDictionary doesn't support expiration eviction natively. 
            // Given the requirement "let the tracker hold it in memory", simple dict is fine.
        }

        public bool Consume(string nodeId, string manifestHash)
        {
            var key = GetKey(nodeId, manifestHash);

            if (_authorizations.TryRemove(key, out var expiresAt))
            {
                if (expiresAt > DateTimeOffset.UtcNow)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetKey(string nodeId, string manifestHash)
        {
            return $"{nodeId}:{manifestHash}";
        }
    }
}
