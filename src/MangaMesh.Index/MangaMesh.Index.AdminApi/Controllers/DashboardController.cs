using MangaMesh.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly INodeRegistry _nodeRegistry;

        public DashboardController(INodeRegistry nodeRegistry)
        {
            _nodeRegistry = nodeRegistry;
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var nodes = _nodeRegistry.GetAllNodes();
            return Ok(new
            {
                ActiveNodes = nodes.Count(n => (DateTime.UtcNow - n.LastSeen).TotalMinutes < 15), // "Online" definition
                TotalPeers = nodes.Count(), // Assuming all are peers for now or check headers? TrackerNode doesn't have Type yet, wait UI has Type.
                // For now just return counts based on what we have.
                Gateways = 0,
                Bootstraps = 0
            });
        }

        [HttpGet("nodes")]
        public IActionResult GetNodes([FromQuery] string? filter)
        {
            var nodes = _nodeRegistry.GetAllNodes();
            // Map to UI model
            var result = nodes.Select(n => new
            {
                id = n.NodeId,
                type = "Peer", // Default
                ip = n.IP,
                port = n.Port,
                lastSeen = n.LastSeen,
                status = (DateTime.UtcNow - n.LastSeen).TotalMinutes < 15 ? "Online" : "Offline",
                version = "1.0.0" 
            });

            return Ok(result);
        }
    }
}
