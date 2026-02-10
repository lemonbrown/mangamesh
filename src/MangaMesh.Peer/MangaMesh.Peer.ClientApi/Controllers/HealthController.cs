using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {

        [HttpGet(Name = "GetHealth")]
        public async Task<IResult> GetHealth()
        {
            return Results.Ok(new
            {
                status = "online",
                time = DateTime.UtcNow
            });
        }
    }
}
