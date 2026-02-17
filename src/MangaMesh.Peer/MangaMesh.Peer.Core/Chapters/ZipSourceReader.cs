using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>Reads image files from a .zip or .cbz archive.</summary>
    public sealed class ZipSourceReader : IChapterSourceReader
    {
        private readonly IImageFormatProvider _formats;

        public ZipSourceReader(IImageFormatProvider formats)
        {
            _formats = formats;
        }

        public bool CanRead(string sourcePath)
        {
            if (!File.Exists(sourcePath)) return false;
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            return ext is ".zip" or ".cbz";
        }

        public async IAsyncEnumerable<(string name, Stream content)> ReadFilesAsync(
            string sourcePath,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var fileStream = File.OpenRead(sourcePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var entries = archive.Entries
                .Where(e => _formats.IsSupported(e.FullName) && e.Length > 0)
                .OrderBy(e => e.FullName)
                .ToArray();

            if (entries.Length == 0)
                throw new InvalidOperationException("No valid image files found in zip archive.");

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                // Copy to MemoryStream: ZipArchive entries are non-seekable and only readable
                // while the archive is open, so we buffer each page before yielding.
                var mem = new MemoryStream();
                using (var entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(mem, ct);
                }
                mem.Position = 0;

                yield return (entry.Name, mem);
            }
        }
    }
}
