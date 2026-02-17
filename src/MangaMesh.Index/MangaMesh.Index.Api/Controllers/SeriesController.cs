
using MangaMesh.Shared.Models;
using MangaMesh.Index.Api.Services;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MangaMesh.Index.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeriesController : ControllerBase
{
    private readonly ISeriesStore _seriesStore;
    private readonly ISeriesRegistry _seriesRegistry;
    private readonly IMangaMetadataProvider _metadataProvider;
    private readonly IManifestEntryStore _manifestEntryStore;
    private readonly INodeRegistry _nodeRegistry;
    private readonly ICoverService _coverService;
    private readonly IConfiguration _configuration;

    public SeriesController(
        ISeriesStore seriesStore,
        ISeriesRegistry seriesRegistry,
        IMangaMetadataProvider metadataProvider,
        IManifestEntryStore manifestEntryStore,
        INodeRegistry nodeRegistry,
        ICoverService coverService,
        IConfiguration configuration)
    {
        _seriesStore = seriesStore;
        _seriesRegistry = seriesRegistry;
        _metadataProvider = metadataProvider;
        _manifestEntryStore = manifestEntryStore;
        _nodeRegistry = nodeRegistry;
        _coverService = coverService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterSeriesResponse>> RegisterSeries([FromBody] RegisterSeriesRequest request)
    {
        // 1. Check if already exists
        var existing = await _seriesRegistry.GetByExternalIdAsync(request.Source, request.ExternalMangaId);
        if (existing != null)
        {
            return Ok(new RegisterSeriesResponse
            {
                SeriesId = existing.SeriesId,
                Title = existing.Title
            });
        }

        // 2. Verify metadata
        var metadata = await _metadataProvider.GetMangaAsync(request.ExternalMangaId);
        if (metadata == null)
        {
            return NotFound($"Manga with ID {request.ExternalMangaId} not found on {request.Source}");
        }

        // 3. Create new
        var newId = $"mm_manga_{NUlid.Ulid.NewUlid()}";
        var def = new SeriesDefinition
        {
            SeriesId = newId,
            Source = request.Source,
            ExternalMangaId = request.ExternalMangaId,
            Title = metadata.CanonicalTitle,
            CreatedUtc = DateTime.UtcNow
        };

        await _seriesRegistry.RegisterAsync(def);

        return Ok(new RegisterSeriesResponse
        {
            SeriesId = newId,
            Title = def.Title
        });
    }

    [HttpGet]
    [ProducesResponseType<List<SeriesSearchResult>>(200)]
    public async Task<IEnumerable<SeriesSummaryResponse>> GetSeries([FromQuery] string? q, [FromQuery] int? limit, [FromQuery] int? offset, [FromQuery] string? sort, [FromQuery] string[]? ids)
    {
        var response = new List<SeriesSummaryResponse>();

        // 1. Search Local Registry (Registered Series)
        // Registry doesn't support sorting/counting natively yet

        bool isSortRequested = !string.IsNullOrWhiteSpace(sort);

        var registry = await _seriesRegistry.GetAllAsync();
        var manifests = await _manifestEntryStore.GetAllAsync();
        var manifestsBySeries = manifests.ToLookup(m => m.SeriesId, StringComparer.OrdinalIgnoreCase);

        var allNodes = _nodeRegistry.GetAllNodes().ToList();

        foreach (var r in registry)
        {
            if (!response.Any(res => res.SeriesId == r.SeriesId))
            {
                if (!string.IsNullOrWhiteSpace(q) && !r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;

                var seriesManifests = manifestsBySeries[r.SeriesId].ToList();
                var stats = seriesManifests.Any()
                    ? new
                    {
                        Count = seriesManifests.Select(m => m.ChapterId).Distinct().Count(),
                        LastUploaded = seriesManifests.Max(m => m.AnnouncedUtc),
                        LatestChapter = seriesManifests.OrderByDescending(m => m.ChapterNumber).FirstOrDefault()
                    }
                    : new
                    {
                        Count = 0,
                        LastUploaded = DateTime.MinValue,
                        LatestChapter = (ManifestEntry?)null
                    };

                var seedCount = allNodes.Count(n => n.ManifestDetails.Values.Any(d => d.SeriesId == r.SeriesId));

                response.Add(new SeriesSummaryResponse
                {
                    SeriesId = r.SeriesId,
                    Title = r.Title,
                    Source = (int)r.Source,
                    ExternalMangaId = r.ExternalMangaId,
                    ChapterCount = stats.Count,
                    LastUploadedAt = stats.LastUploaded,
                    LatestChapterNumber = stats.LatestChapter?.ChapterNumber,
                    LatestChapterTitle = stats.LatestChapter?.Title,
                    SeedCount = seedCount
                });
            }
        }
        return response;
    }

    [HttpGet("{seriesId}")]
    public async Task<ActionResult<SeriesDetailsResponse>> GetSeriesDetails(string seriesId)
    {
        var series = await _seriesStore.GetSeriesDetails(seriesId);

        if (series == null)
        {
            // Fallback to registry (series might be registered but have no chapters yet)
            var def = await _seriesRegistry.GetByIdAsync(seriesId);
            if (def == null)
            {
                return NotFound();
            }

            series = new Series
            {
                SeriesId = def.SeriesId,
                Title = def.Title,
                Source = def.Source,
                ExternalMangaId = def.ExternalMangaId,
                FirstSeenUtc = def.CreatedUtc,
                Chapters = new List<Chapter>()
            };
        }
        else if (string.IsNullOrEmpty(series.ExternalMangaId))
        {
            var def = await _seriesRegistry.GetByIdAsync(seriesId);
            if (def != null)
            {
                series.ExternalMangaId = def.ExternalMangaId;
            }
        }

        if (!string.IsNullOrEmpty(series.ExternalMangaId))
        {
            // Fire and forget or await? Await to ensure image is ready for first view.
            await _coverService.EnsureCoverCachedAsync(series.ExternalMangaId);
        }

        var allNodes = _nodeRegistry.GetAllNodes().ToList();
        var seedCount = allNodes.Count(n => n.ManifestDetails.Values.Any(d => d.SeriesId == seriesId));

        return new SeriesDetailsResponse
        {
            SeriesId = series.SeriesId,
            Title = series.Title,
            ExternalMangaId = series.ExternalMangaId,
            Author = series.Author,
            FirstSeenUtc = series.FirstSeenUtc,
            SeedCount = seedCount
        };
    }

    [HttpGet("{seriesId}/chapters")]
    public async Task<ActionResult<IEnumerable<ChapterSummaryResponse>>> GetSeriesChapters(string seriesId)
    {
        var series = await _seriesStore.GetSeriesDetails(seriesId);

        if (series?.Chapters == null)
        {
            // Fallback: check if series exists in registry
            var def = await _seriesRegistry.GetByIdAsync(seriesId);
            if (def != null)
            {
                return Ok(new List<ChapterSummaryResponse>());
            }
            return NotFound();
        }

        return Ok(series.Chapters.Select(c => new ChapterSummaryResponse
        {
            ChapterId = c.ChapterId,
            ChapterNumber = c.Number,
            Volume = c.Volume,
            Title = c.Title,
            UploadedAt = c.Manifests?.Any() == true ? c.Manifests.Max(m => m.UploadedAt) : null
        }));
    }

    [HttpGet("{seriesId}/chapters/{chapterId}")]
    public async Task<ActionResult<ChapterDetailsResponse>> GetChapterDetails(string seriesId, string chapterId)
    {
        var chapter = await _seriesStore.GetChapterDetails(seriesId, chapterId);

        if (chapter == null)
        {
            return NotFound();
        }

        return new ChapterDetailsResponse
        {
            ChapterId = chapter.ChapterId,
            ChapterNumber = chapter.Number,
            Title = chapter.Title,
            Manifests = chapter.Manifests?.Select(m => new ManifestSummaryResponse
            {
                ManifestHash = m.ManifestHash,
                Language = m.Language,
                ScanGroup = m.ScanGroup,
                Quality = m.Quality,
                UploadedAt = m.UploadedAt
            })
        };
    }

    [HttpGet("{seriesId}/chapter/{chapterId}/manifest/{manifestHash}/read")]
    public IActionResult GetChapterRead(string seriesId, string chapterId, string manifestHash)
    {
        // Redirect to /gateway/ which will be proxied by Komorii's nginx to the Gateway
        return RedirectPreserveMethod($"/gateway/api/read/{manifestHash}");
    }
}
