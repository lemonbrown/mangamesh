namespace MangaMesh.Shared.Models;

public sealed record AnnounceManifestRequest
{
    public string NodeId { get; init; } = "";
    public ManifestHash ManifestHash { get; init; }

    public string SeriesId { get; init; } = "";
    public string ChapterId { get; init; } = "";
    public double ChapterNumber { get; init; }
    public string? Volume { get; init; }
    public ExternalMetadataSource Source { get; init; }
    public string ExternalMangaId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Language { get; init; } = "";
    public string ScanGroup { get; init; } = "";
    public long TotalSize { get; init; }
    public DateTime CreatedUtc { get; init; }
    public List<ChapterFileEntry> Files { get; init; } = new();

    public DateTimeOffset AnnouncedAt { get; init; } = DateTimeOffset.UtcNow;
    public int SchemaVersion { get; init; } = 2;
    public string Signature { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public string SignedBy { get; init; } = "";
    public ReleaseType ReleaseType { get; set; }
}
