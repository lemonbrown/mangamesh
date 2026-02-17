namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>
    /// Strategy for reading chapter image files from a given source path.
    /// Callers must dispose each yielded <see cref="Stream"/> after use.
    /// </summary>
    public interface IChapterSourceReader
    {
        /// <summary>Returns true when this reader can handle the given source path.</summary>
        bool CanRead(string sourcePath);

        /// <summary>Yields (filename, content stream) pairs in natural sort order.</summary>
        IAsyncEnumerable<(string name, Stream content)> ReadFilesAsync(string sourcePath, CancellationToken ct = default);
    }
}
