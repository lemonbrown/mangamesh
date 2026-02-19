using MangaMesh.Peer.GatewayApi.Config;
using MangaMesh.Peer.GatewayApi.Models;
using MangaMesh.Peer.GatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.GatewayApi.Controllers;

[ApiController]
[Route("api")]
public class GatewayController : ControllerBase
{
    private readonly GatewayService _gateway;
    private readonly IConfiguration _configuration;
    private readonly GatewayConfig _config;

    public GatewayController(GatewayService gateway, IConfiguration configuration, GatewayConfig config)
    {
        _gateway = gateway;
        _configuration = configuration;
        _config = config;
    }

    /// <summary>
    /// Fetches a manifest by its hash.
    /// In Proxy mode: returns the manifest JSON directly.
    /// In PeerRedirect mode: returns a JSON list of peer URLs where the manifest can be fetched.
    /// </summary>
    [HttpGet("manifests/{hash}")]
    public async Task<IActionResult> GetManifest(string hash)
    {
        if (_config.Mode == GatewayMode.PeerRedirect)
        {
            var peerUrls = await _gateway.FindPeerUrlsAsync(hash, $"api/manifest/{hash}");
            if (peerUrls.Count == 0) return NotFound("No peers found for this manifest.");
            return Ok(new PeerRedirectResponse(hash, peerUrls));
        }

        var manifest = await _gateway.GetManifestAsync(hash);
        if (manifest == null)
            return NotFound("Manifest not found in mesh or cache.");

        return Ok(manifest);
    }

    /// <summary>
    /// Fetches a chapter manifest and returns its file listing.
    /// In Proxy mode: Nodes contains this gateway's URL — client fetches blobs via this gateway.
    /// In PeerRedirect mode: Nodes contains actual peer base URLs — client fetches blobs directly
    /// from peers using {peerBaseUrl}/api/blob/{fileHash}.
    /// </summary>
    [HttpGet("read/{hash}")]
    public async Task<IActionResult> GetChapterRead(string hash)
    {
        var (manifestData, _) = await _gateway.GetManifestWithNodesAsync(hash);

        if (manifestData == null)
            return NotFound("Manifest not found in mesh or cache.");

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var manifest = System.Text.Json.JsonSerializer.Deserialize<MangaMesh.Shared.Models.ChapterManifest>(manifestData.Data, options);

            if (manifest == null) return NotFound("Failed to deserialize manifest.");

            List<string> nodes;
            if (_config.Mode == GatewayMode.PeerRedirect)
            {
                // Return actual peer base URLs so the client fetches blobs directly
                var peerUrls = await _gateway.FindPeerUrlsAsync(hash, string.Empty);
                nodes = peerUrls
                    .Select(u => u.TrimEnd('/'))
                    .ToList();

                if (nodes.Count == 0)
                    return NotFound("No peers found for this manifest.");
            }
            else
            {
                nodes = new List<string> { _configuration["Gateway:PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}" };
            }

            var response = new
            {
                Version = manifest.SchemaVersion.ToString(),
                SeriesId = manifest.SeriesId,
                ChapterId = manifest.ChapterId,
                ChapterNumber = manifest.ChapterNumber,
                Files = manifest.Files.Select(f => new
                {
                    Hash = f.Hash,
                    Filename = f.Path,
                    Size = f.Size
                }),
                Nodes = nodes
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error processing manifest: {ex.Message}");
        }
    }
}
