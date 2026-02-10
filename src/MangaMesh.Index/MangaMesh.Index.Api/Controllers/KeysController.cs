using MangaMesh.Index.Api.Models;
using MangaMesh.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KeysController : ControllerBase
    {

        private readonly IPublicKeyRegistry _keyRegistry;

        public KeysController(IPublicKeyRegistry keyRegistry)
        {
            _keyRegistry = keyRegistry;
        }

        [HttpPost("{publicKeyBase64}/challenges")]
        public async Task<ActionResult<KeyChallengeResponse>> CreateChallenge(string publicKeyBase64)
        {
            var decodedKey = Uri.UnescapeDataString(publicKeyBase64);
            var challenge = await _keyRegistry.CreateChallengeAsync(decodedKey);
            return Ok(challenge);
        }

        [HttpPost("{publicKeyBase64}/challenges/{challengeId}/verify")]
        public async Task<ActionResult<KeyVerificationResponse>> VerifyChallenge(
            string publicKeyBase64,
            string challengeId,
            [FromBody] KeyVerificationRequest request)
        {
            var bytes = Convert.FromBase64String(request.SignatureBase64);

            var result = await _keyRegistry.VerifyChallengeAsync(challengeId, bytes);

            return Ok(result);
        }
    }
}
