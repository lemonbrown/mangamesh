using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/series")]
    public class SeriesController : ControllerBase
    {
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly IManifestEntryStore _manifestStore;

        public SeriesController(ISeriesRegistry seriesRegistry, IManifestEntryStore manifestStore)
        {
            _seriesRegistry = seriesRegistry;
            _manifestStore = manifestStore;
        }

        [HttpGet]
        public async Task<IActionResult> GetSeries([FromQuery] string? search)
        {
            var seriesList = await _seriesRegistry.GetAllAsync();
            var manifests = await _manifestStore.GetAllAsync();
            
            var result = seriesList.Select(s => {
                var sManifests = manifests.Where(m => m.SeriesId == s.SeriesId).ToList();
                return new
                {
                    id = s.SeriesId,
                    title = s.Title,
                    chapterCount = sManifests.Select(m => m.ChapterNumber).Distinct().Count(),
                    lastUpdated = sManifests.Any() ? sManifests.Max(m => m.AnnouncedUtc) : s.CreatedUtc,
                    source = s.Source.ToString(),
                    manifestCount = sManifests.Count
                };
            });

            if (!string.IsNullOrWhiteSpace(search))
            {
                result = result.Where(s => 
                    s.title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.id.Contains(search, StringComparison.OrdinalIgnoreCase)
                );
            }

            return Ok(result);
        }
    }
}
