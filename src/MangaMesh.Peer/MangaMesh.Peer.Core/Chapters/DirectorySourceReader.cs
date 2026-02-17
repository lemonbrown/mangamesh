using System.Runtime.CompilerServices;

namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>Reads image files directly from a directory on the local file system.</summary>
    public sealed class DirectorySourceReader : IChapterSourceReader
    {
        private readonly IImageFormatProvider _formats;

        public DirectorySourceReader(IImageFormatProvider formats)
        {
            _formats = formats;
        }

        public bool CanRead(string sourcePath) => Directory.Exists(sourcePath);

        public async IAsyncEnumerable<(string name, Stream content)> ReadFilesAsync(
            string sourcePath,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var files = Directory.GetFiles(sourcePath)
                .Where(f => _formats.IsSupported(f))
                .OrderBy(f => f)
                .ToArray();

            if (files.Length == 0)
                throw new InvalidOperationException("No valid image files found in source folder.");

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                yield return (Path.GetFileName(file), File.OpenRead(file));
            }

            await Task.CompletedTask; // satisfies async requirement
        }
    }
}
