using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>Series catalog — registering, searching, and querying series metadata.</summary>
    public interface ISeriesRegistry
    {
        Task<(string SeriesId, string Title)> RegisterSeriesAsync(ExternalMetadataSource source, string externalMangaId);
        Task<IEnumerable<SeriesSummaryResponse>> SearchSeriesAsync(string query, string? sort = null, string[]? ids = null);
        Task<TrackerStats> GetStatsAsync();
    }
}
