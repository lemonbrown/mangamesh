using Microsoft.AspNetCore.Mvc;

using MangaMesh.Shared.Models;
using MangaMesh.Index.Api.Mappers;
using MangaMesh.Index.Api.Services;
using MangaMesh.Index.Api.Models;
using MangaMesh.Shared.Services;

namespace MangaMesh.Index.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MangaMetadataController : ControllerBase
    {

        private readonly IMangaMetadataProvider _metadataProvider;

        public MangaMetadataController(IMangaMetadataProvider metadataProvider)
        {
            _metadataProvider = metadataProvider;
        }

        [HttpGet("search")]
        [ProducesResponseType<List<MangaSearchResult>>(200)]
        public async Task<IResult> SearchMangaAsync(string query)
        {
            //var cachedProvider = new CachedMangaMetadataProvider(
            //    _metadataProvider,
            //    searchTtl: TimeSpan.FromMinutes(15),
            //    metadataTtl: TimeSpan.FromHours(6)
            //);

            //var rateLimitedProvider = new RateLimitedMangaMetadataProvider(
            //    cachedProvider,
            //    maxRequests: 2,
            //    perInterval: TimeSpan.FromSeconds(1)
            //);

            //var results = await rateLimitedProvider.SearchMangaAsync(query);

            var results = await _metadataProvider.SearchMangaAsync(query);


            return Results.Ok(results);
        }

        [HttpGet("{seriesId}")]
        [ProducesResponseType<ChapterListResponse>(200)]
        public async Task<IResult> GetChapters(string seriesId)
        {
            var chapters = await _metadataProvider.GetChaptersAsync(seriesId, "en");

            var response = ChapterResponseMapper.ToListResponse(seriesId, ExternalMetadataSource.MangaDex, "en", chapters);

            return Results.Ok(response);
        }
    }
}
