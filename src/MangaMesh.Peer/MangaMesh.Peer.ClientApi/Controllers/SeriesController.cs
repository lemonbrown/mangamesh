using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MangaMesh.Shared.Models;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Tracker;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeriesController : ControllerBase
    {
        private readonly IManifestStore _manifestStore;
        private readonly ITrackerClient _trackerClient;
        private readonly IBlobStore _blobStore;
        private readonly IPeerFetcher _peerFetcher;

        public SeriesController(IManifestStore manifestStore, ITrackerClient trackerClient, IBlobStore blobStore, IPeerFetcher peerFetcher)
        {
            _manifestStore = manifestStore;
            _trackerClient = trackerClient;
            _blobStore = blobStore;
            _peerFetcher = peerFetcher;
        }

        [HttpGet]
        public async Task<IEnumerable<SeriesSummaryResponse>> Get([FromQuery] string? q, [FromQuery] string? sort)
        {
            return await _trackerClient.SearchSeriesAsync(q ?? "", sort);
        }

        [HttpGet("{seriesId}/chapter/{chapterId}/manifest/{manifestHash}/read")]
        public async Task<IResult> ReadChapter(string seriesId, string chapterId, string manifestHash)
        {
            try
            {
                // Ensure manifest and pages are available locally (fetch from peers if needed)
                var storedHash = await _peerFetcher.FetchManifestAsync(manifestHash);

                // Retrieve the manifest from local store
                var manifest = await _manifestStore.GetAsync(storedHash);

                if (manifest == null)
                {
                    return Results.Problem("Manifest downloaded but not found in store");
                }

                return Results.Ok(manifest);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read chapter: {ex.Message}");
            }
        }

        [HttpGet("manifest/{manifestHash}")]
        public async Task<IResult> GetManifestByHash(string manifestHash)
        {
            var hash = new ManifestHash(manifestHash);
            var manifest = await _manifestStore.GetAsync(hash);

            if (manifest == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(manifest);
        }
    }
}
