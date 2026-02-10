using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface ISeriesRegistry
    {
        Task<SeriesDefinition?> GetByExternalIdAsync(ExternalMetadataSource source, string externalMangaId);
        Task<SeriesDefinition?> GetByIdAsync(string seriesId);
        Task RegisterAsync(SeriesDefinition definition);
        Task<IEnumerable<SeriesDefinition>> GetAllAsync();
    }
}
