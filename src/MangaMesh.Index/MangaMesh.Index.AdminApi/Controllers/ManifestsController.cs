using MangaMesh.Index.AdminApi.Services;
using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/manifests")]
    public class ManifestsController : ControllerBase
    {
        private readonly IManifestEntryStore _manifestStore;
        private readonly IManifestAnnouncerStore _announcerStore;
        private readonly ITrackerService _trackerService;

        public ManifestsController(IManifestEntryStore manifestStore, IManifestAnnouncerStore announcerStore, ITrackerService trackerService)
        {
            _manifestStore = manifestStore;
            _announcerStore = announcerStore;
            _trackerService = trackerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetManifests([FromQuery] string? search)
        {
            var manifests = await _manifestStore.GetAllAsync();
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                manifests = manifests.Where(m => 
                    m.ManifestHash.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    m.SeriesId.Contains(search, StringComparison.OrdinalIgnoreCase)
                );
            }

            // Map to UI model
            var result = manifests.Select(m => new
            {
                hash = m.ManifestHash,
                seriesId = m.SeriesId,
                chapterNumber = m.ChapterNumber,
                volume = m.Volume,
                scanGroup = m.ScanGroup,
                uploadedAt = m.AnnouncedUtc,
                verified = true, // We store verified ones usually
                sizeBytes = 0 // We don't track size in ManifestEntry yet
            });

            return Ok(result);
        }

        [HttpGet("{hash}")]
        public async Task<IActionResult> GetManifest(string hash)
        {
            var decoded = Uri.UnescapeDataString(hash);
            var manifest = await _manifestStore.GetAsync(decoded);
            if (manifest == null) return NotFound();

            // Persisted announcers (survive restarts)
            var persistedAnnouncers = (await _announcerStore.GetByManifestHashAsync(decoded))
                .ToDictionary(a => a.NodeId);

            // Live node registry — catches nodes that announced before the DB tracking was added,
            // and augments persisted records with current nodeType and lastSeen
            var liveNodes = await _trackerService.GetAllNodesAsync();
            var liveIndex = liveNodes
                .Where(n => n.Manifests.Contains(decoded))
                .ToDictionary(n => n.NodeId);

            // Union: all persisted + any live nodes not yet in the DB
            var allNodeIds = persistedAnnouncers.Keys.Union(liveIndex.Keys);

            var announcingNodes = allNodeIds.Select(nodeId =>
            {
                persistedAnnouncers.TryGetValue(nodeId, out var persisted);
                liveIndex.TryGetValue(nodeId, out var live);
                return new
                {
                    nodeId,
                    announcedAt = persisted?.AnnouncedAt ?? live!.LastSeen,
                    nodeType = live?.NodeType,
                    lastSeen = live?.LastSeen as DateTime?
                };
            }).ToList();

            return Ok(new
            {
                hash = manifest.ManifestHash,
                title = manifest.Title,
                seriesId = manifest.SeriesId,
                chapterId = manifest.ChapterId,
                chapterNumber = manifest.ChapterNumber,
                volume = manifest.Volume,
                language = manifest.Language,
                scanGroup = manifest.ScanGroup,
                quality = manifest.Quality,
                externalMetadataSource = manifest.ExternalMetadataSource,
                externalMangaId = manifest.ExteralMetadataMangaId,
                announcedAt = manifest.AnnouncedUtc,
                lastSeenAt = manifest.LastSeenUtc,
                announcingNodes
            });
        }

        [HttpDelete("{hash}")]
        public async Task<IActionResult> DeleteManifest(string hash)
        {
            var decoded = Uri.UnescapeDataString(hash);
            await _manifestStore.DeleteAsync(decoded);
            await _announcerStore.DeleteByManifestHashAsync(decoded);
            return Ok();
        }
    }
}
