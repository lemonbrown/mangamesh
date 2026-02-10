using MangaMesh.Client.Blob;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BlobController : ControllerBase
    {

        private readonly ILogger<BlobController> _logger;

        private readonly IBlobStore _blobStore;

        public BlobController(ILogger<BlobController> logger, IBlobStore blobStore)
        {
            _logger = logger;

            _blobStore = blobStore;
        }

        [HttpGet("{hash}", Name = "GetBlobByHash")]
        public async Task<IResult> GetByHashAsync(string hash)
        {
            var blobHash = new BlobHash(hash);
            if (!_blobStore.Exists(blobHash))
                return Results.NotFound();

            var stream = await _blobStore.OpenReadAsync(blobHash);
            return Results.Stream(stream, "application/octet-stream");
        }
    }
}
