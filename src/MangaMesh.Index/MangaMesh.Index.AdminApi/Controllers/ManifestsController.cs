using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/manifests")]
    public class ManifestsController : ControllerBase
    {
        private readonly IManifestEntryStore _manifestStore;

        public ManifestsController(IManifestEntryStore manifestStore)
        {
            _manifestStore = manifestStore;
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

        [HttpDelete("{hash}")]
        public async Task<IActionResult> DeleteManifest(string hash)
        {
            var decoded = Uri.UnescapeDataString(hash);
            await _manifestStore.DeleteAsync(decoded);
            return Ok();
        }
    }
}
