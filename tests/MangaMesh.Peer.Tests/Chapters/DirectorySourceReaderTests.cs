using MangaMesh.Peer.Core.Chapters;

namespace MangaMesh.Peer.Tests.Chapters;

[TestClass]
public class DirectorySourceReaderTests
{
    private string _tempDir = null!;
    private readonly DefaultImageFormatProvider _formats = new();
    private DirectorySourceReader _reader = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mm_dir_reader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _reader = new DirectorySourceReader(_formats);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteFile(string name, byte[] content)
        => File.WriteAllBytes(Path.Combine(_tempDir, name), content);

    // ── CanRead ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CanRead_ExistingDirectory_ReturnsTrue()
        => Assert.IsTrue(_reader.CanRead(_tempDir));

    [TestMethod]
    public void CanRead_NonExistentPath_ReturnsFalse()
        => Assert.IsFalse(_reader.CanRead(Path.Combine(_tempDir, "does-not-exist")));

    [TestMethod]
    public void CanRead_FilePath_ReturnsFalse()
    {
        var filePath = Path.Combine(_tempDir, "test.jpg");
        File.WriteAllBytes(filePath, new byte[] { 1 });
        Assert.IsFalse(_reader.CanRead(filePath));
    }

    // ── ReadFilesAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReadFilesAsync_ReturnsImageFiles()
    {
        WriteFile("001.jpg", new byte[] { 1, 2, 3 });
        WriteFile("002.png", new byte[] { 4, 5, 6 });

        var files = await CollectAsync(_reader.ReadFilesAsync(_tempDir));

        Assert.AreEqual(2, files.Count);
        var names = files.Select(f => f.name).ToHashSet();
        Assert.IsTrue(names.Contains("001.jpg"));
        Assert.IsTrue(names.Contains("002.png"));
    }

    [TestMethod]
    public async Task ReadFilesAsync_FiltersNonImages()
    {
        WriteFile("001.jpg", new byte[] { 1 });
        WriteFile("notes.txt", new byte[] { 2 });
        WriteFile("script.sh", new byte[] { 3 });

        var files = await CollectAsync(_reader.ReadFilesAsync(_tempDir));

        Assert.AreEqual(1, files.Count);
        Assert.AreEqual("001.jpg", files[0].name);
    }

    [TestMethod]
    public async Task ReadFilesAsync_ReturnedBytesMatchFileContent()
    {
        var expected = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        WriteFile("page.jpg", expected);

        var files = await CollectAsync(_reader.ReadFilesAsync(_tempDir));
        Assert.AreEqual(1, files.Count);

        using var ms = new MemoryStream();
        await files[0].content.CopyToAsync(ms);
        CollectionAssert.AreEqual(expected, ms.ToArray());
    }

    [TestMethod]
    public async Task ReadFilesAsync_SortedByFilename()
    {
        WriteFile("003.jpg", new byte[] { 3 });
        WriteFile("001.jpg", new byte[] { 1 });
        WriteFile("002.jpg", new byte[] { 2 });

        var files = await CollectAsync(_reader.ReadFilesAsync(_tempDir));

        var names = files.Select(f => f.name).ToList();
        Assert.AreEqual("001.jpg", names[0]);
        Assert.AreEqual("002.jpg", names[1]);
        Assert.AreEqual("003.jpg", names[2]);
    }

    [TestMethod]
    public async Task ReadFilesAsync_EmptyDirectory_Throws()
    {
        bool threw = false;
        try
        {
            await CollectAsync(_reader.ReadFilesAsync(_tempDir));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected InvalidOperationException for empty directory");
    }

    [TestMethod]
    public async Task ReadFilesAsync_CancellationRequested_Throws()
    {
        WriteFile("001.jpg", new byte[] { 1 });
        WriteFile("002.jpg", new byte[] { 2 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        bool threw = false;
        try
        {
            await CollectAsync(_reader.ReadFilesAsync(_tempDir, cts.Token));
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected OperationCanceledException when token is already cancelled");
    }

    /// <summary>
    /// Collects all items from the async enumerable, buffering each stream into a
    /// MemoryStream so the underlying file handles are released immediately.
    /// This prevents file-lock errors when the temp directory is cleaned up.
    /// </summary>
    private static async Task<List<(string name, Stream content)>> CollectAsync(
        IAsyncEnumerable<(string name, Stream content)> source)
    {
        var result = new List<(string name, Stream content)>();
        await foreach (var (name, stream) in source)
        {
            var ms = new MemoryStream();
            await using (stream)
                await stream.CopyToAsync(ms);
            ms.Position = 0;
            result.Add((name, ms));
        }
        return result;
    }
}
