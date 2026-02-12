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
            var activeNodes = nodes.Where(n => (DateTime.UtcNow - n.LastSeen).TotalMinutes < 15).ToList();

            return Ok(new
            {
                ActiveNodes = activeNodes.Count,
                TotalPeers = nodes.Count(n => n.NodeType == "Peer"),
                Gateways = nodes.Count(n => n.NodeType == "Gateway"),
                Bootstraps = nodes.Count(n => n.NodeType == "Bootstrap")
            });
        }

        [HttpGet("nodes")]
        public async Task<IActionResult> GetNodes([FromQuery] string? filter)
        {
            var nodes = await _trackerService.GetAllNodesAsync();

            // Filter by type if provided
            if (!string.IsNullOrEmpty(filter) && filter != "All")
            {
                nodes = nodes.Where(n => n.NodeType == filter).ToList();
            }

            // Map to UI model
            var result = nodes.Select(n => new
            {
                id = n.NodeId,
                type = n.NodeType,
                lastSeen = n.LastSeen,
                status = (DateTime.UtcNow - n.LastSeen).TotalMinutes < 15 ? "Online" : "Offline",
                version = "1.0.0"
            });

            return Ok(result);
        }
    }
}
