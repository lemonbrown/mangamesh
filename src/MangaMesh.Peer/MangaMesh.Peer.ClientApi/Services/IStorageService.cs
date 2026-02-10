using MangaMesh.Peer.ClientApi.Models;

namespace MangaMesh.Peer.ClientApi.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Returns total and used disk space and manifest count.
        /// </summary>
        Task<StorageDto> GetStatsAsync(CancellationToken ct = default);
    }

}
