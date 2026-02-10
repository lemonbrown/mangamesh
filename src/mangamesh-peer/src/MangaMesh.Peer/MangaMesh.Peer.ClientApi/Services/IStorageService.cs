using MangaMesh.Server.Models;

namespace MangaMesh.Server.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Returns total and used disk space and manifest count.
        /// </summary>
        Task<StorageDto> GetStatsAsync(CancellationToken ct = default);
    }

}
