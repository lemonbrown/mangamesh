using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface IManifestEntryStore
    {
        Task AddAsync(ManifestEntry entry);
        Task<IEnumerable<ManifestEntry>> GetAllAsync();
        Task<ManifestEntry?> GetAsync(string hash);
    }
}
