using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace MangaMesh.Peer.Tests.Storage;

[TestClass]
public class BlobStoreTests
{
    private string _tempDir = null!;
    private BlobStore _store = null!;
    private Mock<IStorageMonitorService> _monitor = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mm_blob_tests_" + Guid.NewGuid().ToString("N"));
        _monitor = new Mock<IStorageMonitorService>();
        _monitor.Setup(m => m.EnsureStorageAvailable(It.IsAny<long>())).Returns(Task.CompletedTask);
        _store   = CreateStore(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BlobStore CreateStore(string root)
    {
        var opts = Options.Create(new BlobStoreOptions { RootPath = root, MaxStorageBytes = 1024L * 1024 * 1024 });
        return new BlobStore(opts, _monitor.Object);
    }

    private static Stream AsStream(byte[] data) => new MemoryStream(data);

    // ── PutAsync / OpenReadAsync round-trip ──────────────────────────────────

    [TestMethod]
    public async Task PutAsync_ThenOpenReadAsync_RoundTrip()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var hash = await _store.PutAsync(AsStream(data));

        await using var stream = await _store.OpenReadAsync(hash);
        Assert.IsNotNull(stream);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        CollectionAssert.AreEqual(data, ms.ToArray());
    }

    [TestMethod]
    public async Task PutAsync_ReturnedHashIsContentAddressed()
    {
        var data     = new byte[] { 1, 2, 3 };
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();

        var hash = await _store.PutAsync(AsStream(data));

        Assert.AreEqual(expected, hash.Value);
    }

    [TestMethod]
    public async Task PutAsync_SameBytes_ReturnsSameHash_AndIsIdempotent()
    {
        var data  = new byte[] { 7, 8, 9 };
        var hash1 = await _store.PutAsync(AsStream(data));
        var hash2 = await _store.PutAsync(AsStream(data));

        Assert.AreEqual(hash1.Value, hash2.Value);
        // EnsureStorageAvailable called only once — second put short-circuits
        _monitor.Verify(m => m.EnsureStorageAvailable(It.IsAny<long>()), Times.Once);
    }

    [TestMethod]
    public async Task PutAsync_LargeBlob_RoundTrips()
    {
        var data = new byte[2 * 1024 * 1024]; // 2 MB
        new Random(42).NextBytes(data);

        var hash = await _store.PutAsync(AsStream(data));

        await using var stream = await _store.OpenReadAsync(hash);
        Assert.IsNotNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        CollectionAssert.AreEqual(data, ms.ToArray());
    }

    // ── OpenReadAsync miss ───────────────────────────────────────────────────

    [TestMethod]
    public async Task OpenReadAsync_NonExistentHash_ReturnsNull()
    {
        var missing = new BlobHash("aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        var result  = await _store.OpenReadAsync(missing);
        Assert.IsNull(result);
    }

    // ── Exists ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Exists_AfterPut_ReturnsTrue()
    {
        var hash = await _store.PutAsync(AsStream(new byte[] { 1 }));
        Assert.IsTrue(_store.Exists(hash));
    }

    [TestMethod]
    public void Exists_NeverStored_ReturnsFalse()
    {
        var absent = new BlobHash("0000000000000000000000000000000000000000000000000000000000000000");
        Assert.IsFalse(_store.Exists(absent));
    }

    // ── StorageMonitor integration ────────────────────────────────────────────

    [TestMethod]
    public async Task PutAsync_NotifiesMonitorAfterWrite()
    {
        await _store.PutAsync(AsStream(new byte[] { 5, 6, 7 }));
        _monitor.Verify(m => m.NotifyBlobWritten(It.IsAny<long>()), Times.Once);
    }

    [TestMethod]
    public async Task PutAsync_StorageMonitorThrows_PropagatesException()
    {
        _monitor.Setup(m => m.EnsureStorageAvailable(It.IsAny<long>()))
                .ThrowsAsync(new IOException("Storage full"));

        var data  = new byte[] { 99 };
        bool threw = false;
        try { await _store.PutAsync(AsStream(data)); }
        catch (IOException) { threw = true; }
        Assert.IsTrue(threw, "Expected IOException from storage monitor");
    }
}
