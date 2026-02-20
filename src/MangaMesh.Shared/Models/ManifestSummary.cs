namespace MangaMesh.Shared.Models
{
    /// <summary>
    /// Lightweight manifest metadata included in node announce payloads so the tracker
    /// can repopulate ManifestEntry records that were deleted or lost.
    /// </summary>
    public class ManifestSummary
    {
        public string Hash { get; init; } = default!;
        public string SeriesId { get; init; } = default!;
        public string ChapterId { get; init; } = default!;
        public string Title { get; init; } = default!;
        public double ChapterNumber { get; init; }
        public string? Volume { get; init; }
        public string Language { get; init; } = default!;
        public string? ScanGroup { get; init; }
        public string? Quality { get; init; }
        public long TotalSize { get; init; }
        public DateTime CreatedUtc { get; init; }
    }
}
