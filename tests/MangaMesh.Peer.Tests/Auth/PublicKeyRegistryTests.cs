using MangaMesh.Peer.Core.Keys;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using MangaMesh.Peer.Tests.Helpers;

namespace MangaMesh.Peer.Tests.Auth;

[TestClass]
public class PublicKeyRegistryTests
{
    // ── In-memory stores ─────────────────────────────────────────────────────

    private sealed class MemoryChallengeStore : IChallengeStore
    {
        private readonly Dictionary<string, KeyChallenge> _data = new();
        public Task StoreAsync(KeyChallenge c)             { _data[c.Id] = c; return Task.CompletedTask; }
        public Task<KeyChallenge?> GetAsync(string id)     => Task.FromResult(_data.GetValueOrDefault(id));
        public Task DeleteAsync(string id)                 { _data.Remove(id); return Task.CompletedTask; }
        public Task CleanupExpiredAsync()                  => Task.CompletedTask;
    }

    // PublicKeyRecord fields: PublicKeyBase64, RegisteredAt, Revoked
    // PublicKeyRegistry.VerifyChallengeAsync looks up by challenge.UserId which == publicKeyBase64
    private sealed class MemoryPublicKeyStore : IPublicKeyStore
    {
        private readonly Dictionary<string, PublicKeyRecord> _byKey = new();

        public Task StoreAsync(PublicKeyRecord r)
        {
            _byKey[r.PublicKeyBase64] = r;
            return Task.CompletedTask;
        }

        public Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64)
            => Task.FromResult(_byKey.GetValueOrDefault(publicKeyBase64));

        public Task<PublicKeyRecord?> GetByUserIdAsync(string userId)
            => Task.FromResult(_byKey.GetValueOrDefault(userId));

        public Task<PublicKeyRecord?> GetByKeyIdAsync(string id)
            => Task.FromResult<PublicKeyRecord?>(null);

        public Task RevokeAsync(string id) => Task.CompletedTask;

        public Task<IEnumerable<PublicKeyRecord>> GetAllAsync()
            => Task.FromResult(_byKey.Values.AsEnumerable());
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static (PublicKeyRegistry registry, MemoryChallengeStore challenges, MemoryPublicKeyStore keys)
        BuildRegistry()
    {
        var challenges = new MemoryChallengeStore();
        var keys       = new MemoryPublicKeyStore();
        var registry   = new PublicKeyRegistry(keys, challenges, NullLogger<PublicKeyRegistry>.Instance);
        return (registry, challenges, keys);
    }

    private static async Task<(string publicKeyBase64, string privateKeyBase64)> MakeKeyPair()
    {
        var store  = new InMemoryKeyStore();
        var svc    = new KeyPairService(store, NullLogger<KeyPairService>.Instance);
        var result = await svc.GenerateKeyPairBase64Async();
        return (result.PublicKeyBase64, result.PrivateKeyBase64);
    }

    private static PublicKeyRecord MakeRecord(string publicKeyBase64, bool revoked = false) => new()
    {
        PublicKeyBase64 = publicKeyBase64,
        RegisteredAt    = DateTimeOffset.UtcNow,
        Revoked         = revoked
    };

    private static async Task<byte[]> SignNonce(string nonce, string privateKeyBase64)
    {
        var store  = new InMemoryKeyStore();
        var svc    = new KeyPairService(store, NullLogger<KeyPairService>.Instance);
        return Convert.FromBase64String(svc.SolveChallenge(nonce, privateKeyBase64));
    }

    // ── CreateChallengeAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateChallenge_ReturnsIdAndNonce()
    {
        var (registry, _, _) = BuildRegistry();
        var (pubKey, _)      = await MakeKeyPair();

        var response = await registry.CreateChallengeAsync(pubKey);

        Assert.IsFalse(string.IsNullOrEmpty(response.ChallengeId));
        Assert.IsFalse(string.IsNullOrEmpty(response.Nonce));
    }

    [TestMethod]
    public async Task CreateChallenge_StoresChallengeForLaterVerification()
    {
        var (registry, challenges, _) = BuildRegistry();
        var (pubKey, _)               = await MakeKeyPair();

        var response = await registry.CreateChallengeAsync(pubKey);
        var stored   = await challenges.GetAsync(response.ChallengeId);

        Assert.IsNotNull(stored);
        Assert.AreEqual(response.Nonce, stored.Nonce);
    }

    [TestMethod]
    public async Task CreateChallenge_ExpiresInFuture()
    {
        var (registry, _, _) = BuildRegistry();
        var (pubKey, _)      = await MakeKeyPair();

        var response = await registry.CreateChallengeAsync(pubKey);

        Assert.IsTrue(response.ExpiresAt > DateTime.UtcNow);
    }

    // ── VerifyChallengeAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task VerifyChallenge_ValidSignature_ReturnsTrue()
    {
        var (registry, _, keys) = BuildRegistry();
        var (pubKey, privKey)   = await MakeKeyPair();
        await keys.StoreAsync(MakeRecord(pubKey));

        var challenge = await registry.CreateChallengeAsync(pubKey);
        var sigBytes  = await SignNonce(challenge.Nonce, privKey);

        var result = await registry.VerifyChallengeAsync(challenge.ChallengeId, sigBytes);

        Assert.IsTrue(result.Valid);
    }

    [TestMethod]
    public async Task VerifyChallenge_ValidSignature_DeletesChallenge()
    {
        var (registry, challenges, keys) = BuildRegistry();
        var (pubKey, privKey)            = await MakeKeyPair();
        await keys.StoreAsync(MakeRecord(pubKey));

        var challenge = await registry.CreateChallengeAsync(pubKey);
        var sigBytes  = await SignNonce(challenge.Nonce, privKey);

        await registry.VerifyChallengeAsync(challenge.ChallengeId, sigBytes);

        Assert.IsNull(await challenges.GetAsync(challenge.ChallengeId));
    }

    [TestMethod]
    public async Task VerifyChallenge_WrongSignature_ReturnsFalse()
    {
        var (registry, _, keys) = BuildRegistry();
        var (pubKey, _)         = await MakeKeyPair();
        await keys.StoreAsync(MakeRecord(pubKey));

        var challenge = await registry.CreateChallengeAsync(pubKey);
        var badSig    = new byte[64]; // all zeros

        var result = await registry.VerifyChallengeAsync(challenge.ChallengeId, badSig);
        Assert.IsFalse(result.Valid);
    }

    [TestMethod]
    public async Task VerifyChallenge_UnknownChallengeId_ReturnsFalse()
    {
        var (registry, _, _) = BuildRegistry();
        var result = await registry.VerifyChallengeAsync("does-not-exist", new byte[64]);
        Assert.IsFalse(result.Valid);
    }

    [TestMethod]
    public async Task VerifyChallenge_ExpiredChallenge_ReturnsFalse()
    {
        var (registry, challenges, keys) = BuildRegistry();
        var (pubKey, privKey)            = await MakeKeyPair();
        await keys.StoreAsync(MakeRecord(pubKey));

        var expired = new KeyChallenge
        {
            Id        = Guid.NewGuid().ToString("N"),
            UserId    = pubKey,
            Nonce     = Convert.ToBase64String(new byte[32]),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        await challenges.StoreAsync(expired);

        var sigBytes = await SignNonce(expired.Nonce, privKey);
        var result   = await registry.VerifyChallengeAsync(expired.Id, sigBytes);
        Assert.IsFalse(result.Valid);
    }

    [TestMethod]
    public async Task VerifyChallenge_RevokedKey_ReturnsFalse()
    {
        var (registry, _, keys) = BuildRegistry();
        var (pubKey, privKey)   = await MakeKeyPair();
        await keys.StoreAsync(MakeRecord(pubKey, revoked: true));

        var challenge = await registry.CreateChallengeAsync(pubKey);
        var sigBytes  = await SignNonce(challenge.Nonce, privKey);

        var result = await registry.VerifyChallengeAsync(challenge.ChallengeId, sigBytes);
        Assert.IsFalse(result.Valid);
    }

    [TestMethod]
    public async Task VerifyChallenge_KeyNotRegistered_ReturnsFalse()
    {
        var (registry, _, _)  = BuildRegistry();
        var (pubKey, privKey) = await MakeKeyPair();
        // Key NOT stored — registry should return false

        var challenge = await registry.CreateChallengeAsync(pubKey);
        var sigBytes  = await SignNonce(challenge.Nonce, privKey);

        var result = await registry.VerifyChallengeAsync(challenge.ChallengeId, sigBytes);
        Assert.IsFalse(result.Valid);
    }
}
