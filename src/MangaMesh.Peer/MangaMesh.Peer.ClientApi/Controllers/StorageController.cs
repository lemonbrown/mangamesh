using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/node/storage")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        private readonly IStorageMonitorService _storageMonitorService;
        private readonly IBlobStore _blobStore;

        public StorageController(IStorageMonitorService storageMonitorService, IBlobStore blobStore)
        {
            _storageMonitorService = storageMonitorService;
            _blobStore = blobStore;
        }

        [HttpGet]
        public async Task<ActionResult<StorageStats>> GetStorageStats()
        {
            var stats = await _storageMonitorService.GetStorageStatsAsync();
            stats.BlobCount = _blobStore.GetAllHashes().Count();
            return Ok(stats);
        }

        [HttpGet("manifests")]
        public async Task<ActionResult> GetManifests(
            [FromServices] IManifestStore manifestStore,
            [FromQuery] string? q = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 20)
        {
            var all = await manifestStore.GetAllWithDataAsync();

            IEnumerable<(ManifestHash Hash, ChapterManifest Manifest)> filtered = all;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = all.Where(x =>
                    (x.Manifest.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.SeriesId?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.Language?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.ScanGroup?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
            }

            var filteredList = filtered.ToList();
            var total = filteredList.Count;
            var items = filteredList
                .Skip(offset)
                .Take(limit)
                .Select(x => new StoredManifestDto(
                    x.Hash.Value,
                    x.Manifest.SeriesId,
                    x.Manifest.ChapterNumber.ToString(),
                    x.Manifest.Volume,
                    x.Manifest.Language,
                    x.Manifest.ScanGroup,
                    x.Manifest.Title,
                    x.Manifest.TotalSize,
                    x.Manifest.Files?.Count ?? 0,
                    x.Manifest.CreatedUtc))
                .ToList();

            return Ok(new { items, total, offset, limit });
        }

        [HttpDelete("manifests/{hash}")]
        public async Task<ActionResult> DeleteManifest(string hash, [FromServices] IManifestStore manifestStore)
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
                return BadRequest("Invalid hash format");

            await manifestStore.DeleteAsync(manifestHash);
            return Ok();
        }

        [HttpDelete("manifests")]
        public async Task<ActionResult> DeleteManifests([FromBody] string[] hashes, [FromServices] IManifestStore manifestStore)
        {
            foreach (var hash in hashes)
            {
                if (ManifestHash.TryParse(hash, out var manifestHash))
                    await manifestStore.DeleteAsync(manifestHash);
            }
            return Ok();
        }

        [HttpDelete("blobs/{hash}")]
        public ActionResult DeleteBlob(string hash)
        {
            _blobStore.Delete(new BlobHash(hash));
            return Ok();
        }

        [HttpDelete("blobs")]
        public ActionResult DeleteBlobs([FromBody] string[] hashes)
        {
            foreach (var hash in hashes)
                _blobStore.Delete(new BlobHash(hash));
            return Ok();
        }

        [HttpGet("blobs")]
        public ActionResult GetBlobs(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50)
        {
            var all = _blobStore.GetAllHashes().ToList();
            var total = all.Count;
            var items = all
                .Skip(offset)
                .Take(limit)
                .Select(h => new
                {
                    hash = h.Value,
                    sizeBytes = _blobStore.GetSize(h)
                })
                .ToList();

            return Ok(new { items, total, offset, limit });
        }
    }
}
