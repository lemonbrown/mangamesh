using MangaMesh.Peer.Core.Chapters;
using System.IO.Compression;

namespace MangaMesh.Peer.Tests.Chapters;

[TestClass]
public class ZipSourceReaderTests
{
    private string _tempDir = null!;
    private readonly DefaultImageFormatProvider _formats = new();
    private ZipSourceReader _reader = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mm_zip_reader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _reader = new ZipSourceReader(_formats);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string MakeZip(string name, IEnumerable<(string entryName, byte[] content)> entries)
    {
        var path = Path.Combine(_tempDir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            var entry = zip.CreateEntry(entryName);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }
        return path;
    }

    private static async Task<List<(string name, Stream content)>> CollectAsync(
        IAsyncEnumerable<(string name, Stream content)> source)
    {
        var result = new List<(string name, Stream content)>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    // ── CanRead ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CanRead_ZipFile_ReturnsTrue()
    {
        var path = MakeZip("test.zip", new[] { ("001.jpg", new byte[] { 1 }) });
        Assert.IsTrue(_reader.CanRead(path));
    }

    [TestMethod]
    public void CanRead_CbzFile_ReturnsTrue()
    {
        var path = Path.Combine(_tempDir, "test.cbz");
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x05, 0x06 }); // minimal empty zip magic
        Assert.IsTrue(_reader.CanRead(path));
    }

    [TestMethod]
    public void CanRead_NonExistentFile_ReturnsFalse()
        => Assert.IsFalse(_reader.CanRead(Path.Combine(_tempDir, "nope.zip")));

    [TestMethod]
    public void CanRead_DirectoryPath_ReturnsFalse()
        => Assert.IsFalse(_reader.CanRead(_tempDir));

    [TestMethod]
    public void CanRead_NonZipExtension_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "chapter.tar");
        File.WriteAllBytes(path, new byte[] { 1 });
        Assert.IsFalse(_reader.CanRead(path));
    }

    // ── ReadFilesAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReadFilesAsync_ValidZip_ReturnsAllImages()
    {
        var zip = MakeZip("chapter.zip", new[]
        {
            ("001.jpg", new byte[] { 1, 2, 3 }),
            ("002.png", new byte[] { 4, 5, 6 })
        });

        var files = await CollectAsync(_reader.ReadFilesAsync(zip));

        Assert.AreEqual(2, files.Count);
        var names = files.Select(f => f.name).ToHashSet();
        Assert.IsTrue(names.Contains("001.jpg"));
        Assert.IsTrue(names.Contains("002.png"));
    }

    [TestMethod]
    public async Task ReadFilesAsync_FiltersNonImages()
    {
        var zip = MakeZip("chapter.zip", new[]
        {
            ("001.jpg", new byte[] { 1 }),
            ("readme.txt", new byte[] { 2 }),
            ("info.xml", new byte[] { 3 })
        });

        var files = await CollectAsync(_reader.ReadFilesAsync(zip));

        Assert.AreEqual(1, files.Count);
        Assert.AreEqual("001.jpg", files[0].name);
    }

    [TestMethod]
    public async Task ReadFilesAsync_ReturnedBytesMatchZipContent()
    {
        var expected = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG SOI marker
        var zip = MakeZip("chapter.zip", new[] { ("page.jpg", expected) });

        var files = await CollectAsync(_reader.ReadFilesAsync(zip));
        Assert.AreEqual(1, files.Count);

        using var ms = new MemoryStream();
        await files[0].content.CopyToAsync(ms);
        CollectionAssert.AreEqual(expected, ms.ToArray());
    }

    [TestMethod]
    public async Task ReadFilesAsync_SortedByEntryName()
    {
        var zip = MakeZip("chapter.zip", new[]
        {
            ("003.jpg", new byte[] { 3 }),
            ("001.jpg", new byte[] { 1 }),
            ("002.jpg", new byte[] { 2 })
        });

        var files = await CollectAsync(_reader.ReadFilesAsync(zip));
        var names = files.Select(f => f.name).ToList();

        Assert.AreEqual("001.jpg", names[0]);
        Assert.AreEqual("002.jpg", names[1]);
        Assert.AreEqual("003.jpg", names[2]);
    }

    [TestMethod]
    public async Task ReadFilesAsync_EmptyZip_Throws()
    {
        var zip  = MakeZip("empty.zip", Array.Empty<(string, byte[])>());
        bool threw = false;
        try
        {
            await CollectAsync(_reader.ReadFilesAsync(zip));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected InvalidOperationException for zip with no valid images");
    }

    [TestMethod]
    public async Task ReadFilesAsync_ZipWithOnlyNonImages_Throws()
    {
        var zip   = MakeZip("noimg.zip", new[] { ("readme.txt", new byte[] { 1 }) });
        bool threw = false;
        try
        {
            await CollectAsync(_reader.ReadFilesAsync(zip));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected InvalidOperationException when zip contains no image files");
    }

    [TestMethod]
    public async Task ReadFilesAsync_CancellationRequested_Throws()
    {
        var zip = MakeZip("chapter.zip", new[]
        {
            ("001.jpg", new byte[] { 1 }),
            ("002.jpg", new byte[] { 2 })
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        bool threw = false;
        try
        {
            await CollectAsync(_reader.ReadFilesAsync(zip, cts.Token));
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected OperationCanceledException when token already cancelled");
    }
}
