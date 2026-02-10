using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/logs")]
    public class LogsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetLogs([FromQuery] string? level)
        {
            // Mock logs for now as we don't have a persisted log store accessible via API yet
            var logs = new[]
            {
                new { id = 1, timestamp = DateTime.UtcNow.AddMinutes(-1), level = "Info", category = "System", message = "System started" },
                new { id = 2, timestamp = DateTime.UtcNow.AddMinutes(-5), level = "Warning", category = "Network", message = "High latency detected" },
                new { id = 3, timestamp = DateTime.UtcNow.AddMinutes(-10), level = "Error", category = "Auth", message = "Invalid signature challenge" }
            };

            return Ok(logs);
        }
    }
}
