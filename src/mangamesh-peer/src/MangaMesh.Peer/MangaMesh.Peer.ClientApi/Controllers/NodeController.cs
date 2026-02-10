using MangaMesh.Client.Manifests;
using MangaMesh.Client.Node;
using MangaMesh.Client.Tracker;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NodeController : ControllerBase
    {
        private readonly INodeIdentityService _nodeIdentity;
        private readonly ITrackerClient _trackerClient;
        private readonly IManifestStore _manifestStore;

        public NodeController(
            INodeIdentityService nodeIdentity,
            ITrackerClient trackerClient,
            IManifestStore manifestStore)
        {
            _nodeIdentity = nodeIdentity;
            _trackerClient = trackerClient;
            _manifestStore = manifestStore;
        }

        [HttpGet("status")]
        public async Task<IResult> GetStatus()
        {
            // Rely on the background ReplicationService to maintain status
            // var isConnected = await _trackerClient.CheckNodeExistsAsync(_nodeIdentity.NodeId);
            // _nodeIdentity.UpdateStatus(isConnected);
            
            var stats = await _trackerClient.GetStatsAsync();
            var (_, seededCount) = await _manifestStore.GetSetHashAsync();

            return Results.Ok(new
            {
                _nodeIdentity.NodeId,
                _nodeIdentity.IsConnected,
                LastPingUtc = _nodeIdentity.LastPingUtc,
                PeerCount = stats.NodeCount,
                SeededManifests = seededCount
            });
        }
    }
}
