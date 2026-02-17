using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NodeController : ControllerBase
    {
        private readonly INodeIdentityService _nodeIdentity;
        private readonly ISeriesRegistry _trackerClient;
        private readonly IManifestStore _manifestStore;
        private readonly IConfiguration _configuration;

        public NodeController(
            INodeIdentityService nodeIdentity,
            ISeriesRegistry trackerClient,
            IManifestStore manifestStore,
            IConfiguration configuration)
        {
            _nodeIdentity = nodeIdentity;
            _trackerClient = trackerClient;
            _manifestStore = manifestStore;
            _configuration = configuration;
        }

        [HttpGet("status")]
        public async Task<IResult> GetStatus()
        {
            // Rely on the background ReplicationService to maintain status
            // var isConnected = await _trackerClient.CheckNodeExistsAsync(_nodeIdentity.NodeId);
            // _nodeIdentity.UpdateStatus(isConnected);
            
            var stats = await _trackerClient.GetStatsAsync();
            var (_, seededCount) = await _manifestStore.GetSetHashAsync();
            var trackerUrl = _configuration["TrackerUrl"] ?? "https://localhost:7030";

            return Results.Ok(new
            {
                _nodeIdentity.NodeId,
                _nodeIdentity.IsConnected,
                LastPingUtc = _nodeIdentity.LastPingUtc,
                PeerCount = stats.NodeCount,
                SeededManifests = seededCount,
                TrackerUrl = trackerUrl
            });
        }
    }
}
