using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Tracker
{
    public sealed class AnnounceManifestRequest
    {
        // Who is announcing
        public string NodeId { get; init; } = string.Empty;

        // What is being announced
        public ManifestHash ManifestHash { get; init; } = default!;

        // Canon reference (for indexing & querying)
        public string SeriesId { get; init; } = string.Empty;
        public string ChapterId { get; init; } = string.Empty;
        public string Chapter { get; init; } = string.Empty;
        public string? Volume { get; init; }
        public double ChapterNumber { get; init; }
        public Shared.Models.ExternalMetadataSource Source { get; init; }
        public string ExternalMangaId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;

        // Release-specific metadata
        public string Language { get; init; } = string.Empty;
        public string? ScanlatorId { get; init; }
        public string ScanGroup { get; init; } = string.Empty;
        public long TotalSize { get; init; }
        public DateTime CreatedUtc { get; init; }
        public List<Shared.Models.ChapterFileEntry> Files { get; init; } = new();
        public ReleaseType ReleaseType { get; init; }

        // Timestamp (useful for rough ordering, not truth)
        public DateTimeOffset AnnouncedAt { get; init; } = DateTimeOffset.UtcNow;
        public int SchemaVersion { get; init; } = 2;
        public string Signature { get; init; } = string.Empty;
        public string PublicKey { get; init; } = string.Empty;
        public string SignedBy { get; init; } = string.Empty;
    }
}
