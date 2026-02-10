using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public sealed record ChapterManifest
    {
        public int SchemaVersion { get; init; } = 2;
        public string ChapterId { get; init; } = default!;
        public string SeriesId { get; init; } = default!;
        public string Title { get; init; } = default!;
        public string? Volume { get; init; }
        public double ChapterNumber { get; init; }
        public string Language { get; init; } = default!;
        public string ScanGroup { get; init; } = default!;
        public DateTime CreatedUtc { get; init; }
        public IReadOnlyList<ChapterFileEntry> Files { get; init; }
            = Array.Empty<ChapterFileEntry>();
        public long TotalSize { get; init; }
        public string HashAlgorithm { get; init; } = "sha256";
        public string SignedBy { get; set; } = default!;
        public string PublicKey { get; set; } = default!;
        public string Signature { get; set; } = default!;
    }

}
