using MangaMesh.Client.Manifests;
using MangaMesh.Client.Storage;
using MangaMesh.Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MangaMesh.Server.Controllers
{
    [Route("api/node/storage")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        private readonly IStorageMonitorService _storageMonitorService;

        public StorageController(IStorageMonitorService storageMonitorService)
        {
            _storageMonitorService = storageMonitorService;
        }

        [HttpGet]
        public async Task<ActionResult<StorageStats>> GetStorageStats()
        {
            var stats = await _storageMonitorService.GetStorageStatsAsync();
            return Ok(stats);
        }

        [HttpGet("manifests")]
        public async Task<ActionResult<IEnumerable<StoredManifestDto>>> GetManifests([FromServices] IManifestStore manifestStore)
        {
            // Note: Loading all manifests might be heavy if there are thousands.
            // Pagination should be considered for scaling, but for now this suffices.
            var hashes = await manifestStore.GetAllHashesAsync();
            var result = new List<StoredManifestDto>();

            foreach (var hash in hashes)
            {
                var manifest = await manifestStore.GetAsync(hash);
                if (manifest != null)
                {
                    result.Add(new StoredManifestDto(
                        hash.Value,
                        manifest.SeriesId,
                        manifest.ChapterNumber.ToString(), // Simple conversion
                        manifest.Volume,
                        manifest.Language,
                        manifest.ScanGroup,
                        manifest.Title,
                        manifest.TotalSize,
                        manifest.Files?.Count ?? 0,
                        manifest.CreatedUtc
                    ));
                }
            }

            // Sort by CreatedUtc desc
            return Ok(result.OrderByDescending(m => m.CreatedUtc));
        }

        [HttpDelete("manifests/{hash}")]
        public async Task<ActionResult> DeleteManifest(string hash, [FromServices] IManifestStore manifestStore)
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
            {
                return BadRequest("Invalid hash format");
            }

            await manifestStore.DeleteAsync(manifestHash);
            return Ok();
        }
    }
}
