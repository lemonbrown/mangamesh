using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Tests.Storage;

[TestClass]
public class ManifestStoreTests
{
    private string _tempDir = null!;
    private ManifestStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mm_manifest_tests_" + Guid.NewGuid().ToString("N"));
        var opts = Options.Create(new ManifestStoreOptions { RootPath = _tempDir });
        _store   = new ManifestStore(opts);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static ChapterManifest MakeManifest(string chapterId = "ch-1", string seriesId = "s-1") => new()
    {
        ChapterId     = chapterId,
        SeriesId      = seriesId,
        Title         = "Test Chapter",
        ChapterNumber = 1.0,
        Language      = "en",
        ScanGroup     = "group",
        CreatedUtc    = DateTime.UtcNow
    };

    private static ManifestHash HashFor(string value) => ManifestHash.Parse(
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant());

    // ── SaveAsync / LoadAsync round-trip ─────────────────────────────────────

    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTrip()
    {
        var manifest = MakeManifest();
        var hash     = HashFor("test-hash-1");

        await _store.SaveAsync(hash, manifest);
        var loaded = await _store.LoadAsync(hash);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(manifest.ChapterId,     loaded.ChapterId);
        Assert.AreEqual(manifest.SeriesId,      loaded.SeriesId);
        Assert.AreEqual(manifest.ChapterNumber, loaded.ChapterNumber);
    }

    [TestMethod]
    public async Task LoadAsync_MissingHash_ReturnsNull()
    {
        var missing = HashFor("does-not-exist");
        var result  = await _store.LoadAsync(missing);
        Assert.IsNull(result);
    }

    // ── ExistsAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ExistsAsync_AfterSave_ReturnsTrue()
    {
        var hash = HashFor("exists-test");
        await _store.SaveAsync(hash, MakeManifest());
        Assert.IsTrue(await _store.ExistsAsync(hash));
    }

    [TestMethod]
    public async Task ExistsAsync_NotSaved_ReturnsFalse()
    {
        Assert.IsFalse(await _store.ExistsAsync(HashFor("never-saved")));
    }

    // ── GetAllHashesAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAllHashesAsync_ReturnsAllSaved()
    {
        var h1 = HashFor("hash-a");
        var h2 = HashFor("hash-b");
        await _store.SaveAsync(h1, MakeManifest("ch-a"));
        await _store.SaveAsync(h2, MakeManifest("ch-b"));

        var all    = (await _store.GetAllHashesAsync()).ToList();
        var values = all.Select(h => h.Value).ToHashSet();

        Assert.IsTrue(values.Contains(h1.Value));
        Assert.IsTrue(values.Contains(h2.Value));
    }

    [TestMethod]
    public async Task GetAllHashesAsync_EmptyStore_ReturnsEmpty()
    {
        var all = await _store.GetAllHashesAsync();
        Assert.IsFalse(all.Any());
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_RemovesManifest()
    {
        var hash = HashFor("to-delete");
        await _store.SaveAsync(hash, MakeManifest());
        Assert.IsTrue(await _store.ExistsAsync(hash));

        await _store.DeleteAsync(hash);
        Assert.IsFalse(await _store.ExistsAsync(hash));
    }

    [TestMethod]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        await _store.DeleteAsync(HashFor("phantom"));
    }

    [TestMethod]
    public async Task DeleteAsync_RemovedFromGetAllHashes()
    {
        var hash = HashFor("delete-index");
        await _store.SaveAsync(hash, MakeManifest());
        await _store.DeleteAsync(hash);

        var all = (await _store.GetAllHashesAsync()).Select(h => h.Value).ToHashSet();
        Assert.IsFalse(all.Contains(hash.Value));
    }

    // ── SaveAsync idempotency ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveAsync_DuplicateHash_Overwrites()
    {
        var hash = HashFor("overwrite-me");
        await _store.SaveAsync(hash, MakeManifest("original"));

        var updated = MakeManifest("updated");
        await _store.SaveAsync(hash, updated);

        var loaded = await _store.LoadAsync(hash);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("updated", loaded.ChapterId);
    }

    // ── GetBySeriesAndChapterIdAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task GetBySeriesAndChapterIdAsync_MatchFound_ReturnsManifest()
    {
        var hash     = HashFor("series-ch-lookup");
        var manifest = MakeManifest("ch-42", "series-x");
        await _store.SaveAsync(hash, manifest);

        var result = await _store.GetBySeriesAndChapterIdAsync("series-x", "ch-42");
        Assert.IsNotNull(result);
        Assert.AreEqual("ch-42",    result.ChapterId);
        Assert.AreEqual("series-x", result.SeriesId);
    }

    [TestMethod]
    public async Task GetBySeriesAndChapterIdAsync_NoMatch_ReturnsNull()
    {
        var result = await _store.GetBySeriesAndChapterIdAsync("nonexistent", "ch-0");
        Assert.IsNull(result);
    }
}
