using MangaMesh.Peer.ClientApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("api/node/logs")]
    public class LogsController : ControllerBase
    {
        private readonly InMemoryLoggerProvider _loggerProvider;

        public LogsController(InMemoryLoggerProvider loggerProvider)
        {
            _loggerProvider = loggerProvider;
        }

        [HttpGet]
        public IEnumerable<LogEntry> GetLogs([FromQuery] int? minLevel = null)
        {
            var logs = _loggerProvider.GetLogs().OrderByDescending(l => l.Timestamp);
            if (minLevel.HasValue)
                return logs.Where(l => (int)l.Level >= minLevel.Value);
            return logs;
        }

        [HttpDelete]
        public IActionResult ClearLogs()
        {
            _loggerProvider.Clear();
            return Ok();
        }
    }
}
