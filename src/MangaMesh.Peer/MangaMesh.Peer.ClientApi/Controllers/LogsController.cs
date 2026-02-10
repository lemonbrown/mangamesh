using MangaMesh.Peer.ClientApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("logs")]
    public class LogsController : ControllerBase
    {
        private readonly InMemoryLoggerProvider _loggerProvider;

        public LogsController(InMemoryLoggerProvider loggerProvider)
        {
            _loggerProvider = loggerProvider;
        }

        [HttpGet]
        public IEnumerable<LogEntry> GetLogs()
        {
            return _loggerProvider.GetLogs().OrderByDescending(l => l.Timestamp);
        }
    }
}
