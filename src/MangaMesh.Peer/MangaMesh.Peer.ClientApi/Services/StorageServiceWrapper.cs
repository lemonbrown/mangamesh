using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.Core.Manifests;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class StorageServiceWrapper : IStorageService
    {
        private readonly ManifestStore _manifestStore;
        private readonly string _storageRoot;

        public StorageServiceWrapper(ManifestStore manifestStore, string storageRoot)
        {
            _manifestStore = manifestStore;
            _storageRoot = storageRoot;
        }

        public async Task<StorageDto> GetStatsAsync(CancellationToken ct = default)
        {
            var manifests = await _manifestStore.GetAllHashesAsync();
            var manifestCount = manifests.Count();

            var dirInfo = new DirectoryInfo(_storageRoot);
            long usedBytes = dirInfo.Exists ? dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            long usedMb = usedBytes / (1024 * 1024);

            // assume total disk space 50GB for now
            long totalMb = 50_000;

            return new StorageDto(totalMb, usedMb, manifestCount);
        }
    }

}
