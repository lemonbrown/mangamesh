using MangaMesh.Peer.Core.Blob;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
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

        /// <summary>
        /// Reassembles a file from a PageManifest hash stored locally.
        /// Reads the PageManifest blob, fetches all chunks from local blob store, and returns the assembled file.
        /// Used by clients in PeerRedirect gateway mode to fetch content directly from a peer.
        /// </summary>
        [HttpGet("~/api/file/{pageHash}", Name = "GetFileByPageHash")]
        public async Task<IActionResult> GetFileByPageHashAsync(string pageHash)
        {
            var manifestHash = new BlobHash(pageHash);
            if (!_blobStore.Exists(manifestHash))
                return NotFound();

            PageManifest? manifest;
            try
            {
                await using var manifestStream = await _blobStore.OpenReadAsync(manifestHash);
                manifest = await JsonSerializer.DeserializeAsync<PageManifest>(manifestStream);
                if (manifest == null) return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize PageManifest for {PageHash}", pageHash);
                return BadRequest("Invalid PageManifest.");
            }

            if (manifest.FileSize > 100 * 1024 * 1024)
                return BadRequest("File too large for in-memory reassembly.");

            var fileData = new byte[manifest.FileSize];
            int offset = 0;

            foreach (var chunkHash in manifest.Chunks)
            {
                var chunk = new BlobHash(chunkHash);
                if (!_blobStore.Exists(chunk))
                {
                    _logger.LogWarning("Missing chunk {ChunkHash} for page {PageHash}", chunkHash, pageHash);
                    return NotFound($"Missing chunk {chunkHash}.");
                }

                await using var chunkStream = await _blobStore.OpenReadAsync(chunk);
                var chunkBytes = new byte[chunkStream.Length];
                await chunkStream.ReadExactlyAsync(chunkBytes);

                if (offset + chunkBytes.Length > fileData.Length)
                    return BadRequest("Chunk data exceeds declared file size.");

                chunkBytes.CopyTo(fileData, offset);
                offset += chunkBytes.Length;
            }

            return File(fileData, manifest.MimeType);
        }
    }
}
