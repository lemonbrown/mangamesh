using MangaMesh.Peer.Core.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using MangaMesh.Peer.Tests.Helpers;

namespace MangaMesh.Peer.Tests.Keys;

[TestClass]
public class KeyPairServiceTests
{
    private KeyPairService CreateService(IKeyStore? store = null)
        => new(store ?? new InMemoryKeyStore(), NullLogger<KeyPairService>.Instance);

    // ── GenerateKeyPairBase64 ────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateKeyPairBase64Async_ProducesValidBase64Keys()
    {
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PublicKeyBase64));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PrivateKeyBase64));

        // Must be valid Base64
        Convert.FromBase64String(result.PublicKeyBase64);
        Convert.FromBase64String(result.PrivateKeyBase64);
    }

    [TestMethod]
    public async Task GenerateKeyPairBase64Async_TwoCalls_ProduceDifferentKeys()
    {
        var svc = CreateService();
        var r1  = await svc.GenerateKeyPairBase64Async();
        var r2  = await svc.GenerateKeyPairBase64Async();

        Assert.AreNotEqual(r1.PublicKeyBase64,  r2.PublicKeyBase64);
        Assert.AreNotEqual(r1.PrivateKeyBase64, r2.PrivateKeyBase64);
    }

    [TestMethod]
    public async Task GenerateKeyPairBase64Async_PersistsToKeyStore()
    {
        var store  = new InMemoryKeyStore();
        var svc    = CreateService(store);
        var result = await svc.GenerateKeyPairBase64Async();

        var stored = await store.GetAsync();
        Assert.IsNotNull(stored);
        Assert.AreEqual(result.PublicKeyBase64,  stored.PublicKeyBase64);
        Assert.AreEqual(result.PrivateKeyBase64, stored.PrivateKeyBase64);
    }

    // ── SolveChallenge ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SolveChallenge_ProducesVerifiableSignature()
    {
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();

        var nonce     = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var signature = svc.SolveChallenge(nonce, result.PrivateKeyBase64);

        Assert.IsFalse(string.IsNullOrEmpty(signature));

        // Should verify using Verify()
        bool valid = svc.Verify(result.PublicKeyBase64, signature, nonce);
        Assert.IsTrue(valid);
    }

    [TestMethod]
    public async Task SolveChallenge_DifferentNonces_ProduceDifferentSignatures()
    {
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();

        var nonce1 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var nonce2 = Convert.ToBase64String(new byte[] { 4, 5, 6 });

        var sig1 = svc.SolveChallenge(nonce1, result.PrivateKeyBase64);
        var sig2 = svc.SolveChallenge(nonce2, result.PrivateKeyBase64);

        Assert.AreNotEqual(sig1, sig2);
    }

    [TestMethod]
    public async Task SolveChallenge_SameInput_ProducesSameSignature()
    {
        // Ed25519 is deterministic
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();
        var nonce  = Convert.ToBase64String(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        var sig1 = svc.SolveChallenge(nonce, result.PrivateKeyBase64);
        var sig2 = svc.SolveChallenge(nonce, result.PrivateKeyBase64);

        Assert.AreEqual(sig1, sig2);
    }

    // ── Verify ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Verify_ValidSignature_ReturnsTrue()
    {
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();
        var nonce  = Convert.ToBase64String(new byte[] { 9, 8, 7, 6 });
        var sig    = svc.SolveChallenge(nonce, result.PrivateKeyBase64);

        Assert.IsTrue(svc.Verify(result.PublicKeyBase64, sig, nonce));
    }

    [TestMethod]
    public async Task Verify_TamperedNonce_ReturnsFalse()
    {
        var svc    = CreateService();
        var result = await svc.GenerateKeyPairBase64Async();
        var nonce  = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var sig    = svc.SolveChallenge(nonce, result.PrivateKeyBase64);

        var differentNonce = Convert.ToBase64String(new byte[] { 9, 9, 9 });
        Assert.IsFalse(svc.Verify(result.PublicKeyBase64, sig, differentNonce));
    }

    [TestMethod]
    public async Task Verify_WrongPublicKey_ReturnsFalse()
    {
        var svc     = CreateService();
        var keyA    = await svc.GenerateKeyPairBase64Async();
        var keyB    = await svc.GenerateKeyPairBase64Async();
        var nonce   = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var sig     = svc.SolveChallenge(nonce, keyA.PrivateKeyBase64);

        Assert.IsFalse(svc.Verify(keyB.PublicKeyBase64, sig, nonce));
    }

    [TestMethod]
    public void Verify_InvalidBase64_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.IsFalse(svc.Verify("!!!notbase64!!!", "AAAA", "AAAA"));
    }
}
