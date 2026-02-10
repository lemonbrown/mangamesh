using MangaMesh.Peer.GatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.GatewayApi.Controllers;

[ApiController]
[Route("content")]
public class ContentController : ControllerBase
{
    private readonly GatewayService _service;

    public ContentController(GatewayService service)
    {
        _service = service;
    }

    /// <summary>
    /// Fetches a raw blob (chunk or manifest JSON) by its SHA-256 hash.
    /// </summary>
    [HttpGet("blob/{hash}")]
    public async Task<IActionResult> GetBlob(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return BadRequest("Hash is required");

        var data = await _service.GetBlobAsync(hash);
        if (data == null) return NotFound();
        
        return File(data, "application/octet-stream");
    }

    /// <summary>
    /// Reassembles a file (e.g. image) from a PageManifest hash.
    /// Fetches the PageManifest, then all chunks, and combines them.
    /// </summary>
    [HttpGet("file/{pageHash}")]
    public async Task<IActionResult> GetFile(string pageHash)
    {
        if (string.IsNullOrEmpty(pageHash)) return BadRequest("PageHash is required");

        var (data, mimeType) = await _service.GetReassembledFileAsync(pageHash);
        if (data == null) return NotFound();
        
        return File(data, mimeType ?? "application/octet-stream");
    }
}
