using MangaMesh.Client.Replication;
using MangaMesh.Server.Models;

namespace MangaMesh.Server.Services
{
    public class NodeStatusService : INodeStatusService
    {
        private readonly ReplicationService _replicationService;
        private readonly IStorageService _storage;

        public NodeStatusService(ReplicationService replicationService, IStorageService storage)
        {
            _replicationService = replicationService;
            _storage = storage;
        }

        public async Task<NodeStatusDto> GetStatusAsync(CancellationToken ct = default)
        {
            var peerCount = _replicationService.GetConnectedPeersCount();
            //var seededManifests = await _replicationService.GetSeededManifestCountAsync(ct);
            var storageStats = await _storage.GetStatsAsync(ct);

            return new NodeStatusDto(
                NodeId: _replicationService.NodeId,
                PeerCount: peerCount,
                SeededManifests: 0,
                StorageUsedMb: storageStats.UsedMb
            );
        }
    }

}
