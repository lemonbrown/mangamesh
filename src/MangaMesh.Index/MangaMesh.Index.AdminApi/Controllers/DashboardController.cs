using MangaMesh.Index.AdminApi.Services;
using MangaMesh.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly ITrackerService _trackerService;

        public DashboardController(ITrackerService trackerService)
        {
            _trackerService = trackerService;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var nodes = await _trackerService.GetAllNodesAsync();
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
        public async Task<IActionResult> GetNodes([FromQuery] string? filter)
        {
            var nodes = await _trackerService.GetAllNodesAsync();
            // Map to UI model
            var result = nodes.Select(n => new
            {
                id = n.NodeId,
                type = "Peer", // Default
                lastSeen = n.LastSeen,
                status = (DateTime.UtcNow - n.LastSeen).TotalMinutes < 15 ? "Online" : "Offline",
                version = "1.0.0" 
            });

            return Ok(result);
        }
    }
}
