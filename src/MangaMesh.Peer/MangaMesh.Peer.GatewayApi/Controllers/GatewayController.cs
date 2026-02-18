using MangaMesh.Peer.GatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.GatewayApi.Controllers;

[ApiController]
[Route("api")]
public class GatewayController : ControllerBase
{
    private readonly GatewayService _gateway;
    private readonly IConfiguration _configuration;

    public GatewayController(GatewayService gateway, IConfiguration configuration)
    {
        _gateway = gateway;
        _configuration = configuration;
    }

    [HttpGet("manifests/{hash}")]
    public async Task<IActionResult> GetManifest(string hash)
    {
        var manifest = await _gateway.GetManifestAsync(hash);
        if (manifest == null)
            return NotFound("Manifest not found in mesh or cache.");

        return Ok(manifest);
    }

    [HttpGet("read/{hash}")]
    public async Task<IActionResult> GetChapterRead(string hash)
    {
        var (manifestData, nodes) = await _gateway.GetManifestWithNodesAsync(hash);

        if (manifestData == null)
            return NotFound("Manifest not found in mesh or cache.");

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var manifest = System.Text.Json.JsonSerializer.Deserialize<MangaMesh.Shared.Models.ChapterManifest>(manifestData.Data, options);

            if (manifest == null) return NotFound("Failed to deserialize manifest.");

            // Map to FullChapterManifest structure for frontend
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
                Nodes = new List<string> { _configuration["Gateway:PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}" }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error processing manifest: {ex.Message}");
        }
    }
}
