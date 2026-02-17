using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace MangaMesh.Peer.Tests.Content;

[TestClass]
public class ChunkIngesterTests
{
    private string _tempDir = null!;
    private BlobStore _blobStore = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mm_chunk_tests_" + Guid.NewGuid().ToString("N"));
        var monitor = new Mock<IStorageMonitorService>();
        monitor.Setup(m => m.EnsureStorageAvailable(It.IsAny<long>())).Returns(Task.CompletedTask);

        var opts  = Options.Create(new BlobStoreOptions { RootPath = _tempDir });
        _blobStore = new BlobStore(opts, monitor.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private ChunkIngester CreateIngester(int chunkSize = 256 * 1024)
    {
        var opts = Options.Create(new DhtOptions { ChunkSizeBytes = chunkSize });
        return new ChunkIngester(_blobStore, opts);
    }

    private static MemoryStream AsStream(byte[] data)
    {
        var ms = new MemoryStream(data);
        ms.Position = 0;
        return ms;
    }

    // ── Small file (single chunk) ─────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_SmallFile_ProducesSingleChunk()
    {
        var ingester = CreateIngester(chunkSize: 1024);
        var data     = new byte[] { 1, 2, 3, 4, 5 };

        var (manifest, _) = await ingester.IngestAsync(AsStream(data), "image/jpeg");

        Assert.AreEqual(1, manifest.Chunks.Count);
        Assert.AreEqual(data.Length, manifest.FileSize);
        Assert.AreEqual("image/jpeg", manifest.MimeType);
    }

    // ── Large file (multiple chunks) ──────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_FileExceedsChunkSize_ProducesMultipleChunks()
    {
        const int chunkSize = 100;
        var ingester = CreateIngester(chunkSize);
        var data     = new byte[350]; // 3 full chunks + 1 partial
        new Random(1).NextBytes(data);

        var (manifest, _) = await ingester.IngestAsync(AsStream(data), "image/png");

        Assert.AreEqual(4, manifest.Chunks.Count);
        Assert.AreEqual(data.Length, manifest.FileSize);
    }

    // ── Content addressing ────────────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_SameBytes_ProduceSameChunkHashes()
    {
        var ingester = CreateIngester(chunkSize: 10);
        var data     = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        var (m1, hash1) = await ingester.IngestAsync(AsStream(data), "image/png");
        var (m2, hash2) = await ingester.IngestAsync(AsStream(data), "image/png");

        Assert.AreEqual(hash1, hash2);
        CollectionAssert.AreEqual(m1.Chunks.ToList(), m2.Chunks.ToList());
    }

    // ── All chunks stored in BlobStore ────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_AllChunksStoredInBlobStore()
    {
        const int chunkSize = 50;
        var ingester = CreateIngester(chunkSize);
        var data     = new byte[120];
        new Random(99).NextBytes(data);

        var (manifest, _) = await ingester.IngestAsync(AsStream(data), "image/jpeg");

        foreach (var chunkHash in manifest.Chunks)
        {
            Assert.IsTrue(_blobStore.Exists(new BlobHash(chunkHash)),
                          $"Chunk {chunkHash} was not stored in BlobStore");
        }
    }

    // ── Manifest itself is stored ─────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_ManifestBlobIsStoredInBlobStore()
    {
        var ingester = CreateIngester();
        var data     = new byte[] { 0xAA, 0xBB, 0xCC };

        var (_, manifestHash) = await ingester.IngestAsync(AsStream(data), "image/webp");

        // The manifest JSON is stored as a blob whose SHA-256 is manifestHash
        Assert.IsTrue(_blobStore.Exists(new BlobHash(manifestHash)),
                      "Manifest blob must be stored in BlobStore");
    }

    // ── Manifest hash correctness ─────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_ManifestHashMatchesBlobContent()
    {
        var ingester  = CreateIngester();
        var data      = new byte[] { 1, 1, 1 };

        var (_, manifestHash) = await ingester.IngestAsync(AsStream(data), "image/jpeg");

        // Read the blob and verify its SHA-256 matches the returned manifest hash
        await using var blobStream = await _blobStore.OpenReadAsync(new BlobHash(manifestHash));
        Assert.IsNotNull(blobStream);
        using var ms = new MemoryStream();
        await blobStream.CopyToAsync(ms);
        var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray())).ToLowerInvariant();

        Assert.AreEqual(manifestHash, actualHash);
    }

    // ── ChunkSize reflected in manifest ───────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_ManifestRecordsCorrectChunkSize()
    {
        const int chunkSize = 512;
        var ingester        = CreateIngester(chunkSize);
        var data            = new byte[1024];

        var (manifest, _) = await ingester.IngestAsync(AsStream(data), "image/png");

        Assert.AreEqual(chunkSize, manifest.ChunkSize);
    }
}
