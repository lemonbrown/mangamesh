using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ManifestController : ControllerBase
    {

        private readonly IManifestStore _manifestStore;

        public ManifestController(IManifestStore manifestStore)
        {
            _manifestStore = manifestStore;
        }

        [HttpGet("{hash}", Name = "GetManifestByHash")]
        public async Task<IResult> GetByHashAsync(string hash)
        {
            var manifestHash = new ManifestHash(hash);
            var manifest = await _manifestStore.GetAsync(manifestHash);

            if (manifest is null)
                return Results.NotFound();

            return Results.Json(manifest);
        }

    }
}
