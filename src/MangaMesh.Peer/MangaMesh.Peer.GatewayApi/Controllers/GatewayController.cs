using MangaMesh.Peer.GatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.GatewayApi.Controllers;

[ApiController]
[Route("api")]
public class GatewayController : ControllerBase
{
    private readonly GatewayService _gateway;

    public GatewayController(GatewayService gateway)
    {
        _gateway = gateway;
    }

    [HttpGet("manifests/{hash}")]
    public async Task<IActionResult> GetManifest(string hash)
    {
        var manifest = await _gateway.GetManifestAsync(hash);
        if (manifest == null)
            return NotFound("Manifest not found in mesh or cache.");

        return Ok(manifest);
    }
}
