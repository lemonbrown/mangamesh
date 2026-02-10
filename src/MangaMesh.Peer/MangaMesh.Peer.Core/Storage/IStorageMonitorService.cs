using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Storage
{
    public interface IStorageMonitorService
    {
        Task<StorageStats> GetStorageStatsAsync();
        Task EnsureStorageAvailable(long bytesRequired);
        void NotifyBlobWritten(long bytes);
    }
}
