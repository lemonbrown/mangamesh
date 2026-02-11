using System.Collections.Concurrent;

namespace MangaMesh.Shared.Models
{
    public sealed record TrackerNode
    {
        public string NodeId { get; init; } = "";
        public HashSet<string> Manifests { get; init; } = new();
        public ConcurrentDictionary<string, (string SeriesId, double ChapterNumber)> ManifestDetails { get; } = new();

        public string ManifestSetHash { get; set; } = "";
        public int ManifestCount { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
