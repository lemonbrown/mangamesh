using MangaMesh.Peer.GatewayApi.Config;
using MangaMesh.Peer.GatewayApi.Models;
using MangaMesh.Peer.GatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.GatewayApi.Controllers;

[ApiController]
[Route("api")]
public class ContentController : ControllerBase
{
    private readonly GatewayService _service;
    private readonly GatewayConfig _config;

    public ContentController(GatewayService service, GatewayConfig config)
    {
        _service = service;
        _config = config;
    }

    /// <summary>
    /// Fetches a raw blob (chunk or manifest JSON) by its SHA-256 hash.
    /// In Proxy mode: streams the blob bytes directly.
    /// In PeerRedirect mode: returns a JSON list of peer URLs where the blob can be fetched.
    /// </summary>
    [HttpGet("blob/{hash}")]
    public async Task<IActionResult> GetBlob(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return BadRequest("Hash is required");

        if (_config.Mode == GatewayMode.PeerRedirect)
        {
            var peerUrls = await _service.FindPeerUrlsAsync(hash, $"api/blob/{hash}");
            if (peerUrls.Count == 0) return NotFound();
            return Ok(new PeerRedirectResponse(hash, peerUrls));
        }

        var data = await _service.GetBlobAsync(hash);
        if (data == null) return NotFound();

        return File(data, "application/octet-stream");
    }

    /// <summary>
    /// Reassembles a file (e.g. image) from a PageManifest hash.
    /// In Proxy mode: fetches the PageManifest, then all chunks, and streams the assembled file.
    /// In PeerRedirect mode: returns peer URLs for the PageManifest blob; client must
    /// fetch and parse the PageManifest then reassemble chunks from peers itself.
    /// </summary>
    [HttpGet("file/{pageHash}")]
    public async Task<IActionResult> GetFile(string pageHash)
    {
        if (string.IsNullOrEmpty(pageHash)) return BadRequest("PageHash is required");

        if (_config.Mode == GatewayMode.PeerRedirect)
        {
            var peerUrls = await _service.FindPeerUrlsAsync(pageHash, $"api/blob/{pageHash}");
            if (peerUrls.Count == 0) return NotFound();
            return Ok(new PeerRedirectResponse(pageHash, peerUrls));
        }

        var (data, mimeType) = await _service.GetReassembledFileAsync(pageHash);
        if (data == null) return NotFound();

        return File(data, mimeType ?? "application/octet-stream");
    }
}
