using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Tracker;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/[controller]")]
    public class KeysController : ControllerBase
    {
        private readonly IKeyPairService _keyPairService;
        private readonly IKeyStore _keyStore;
        private readonly ITrackerClient _trackerClient;

        public KeysController(
            IKeyPairService keyPairService,
            IKeyStore keyStore,
            ITrackerClient trackerClient)
        {
            _keyPairService = keyPairService;
            _keyStore = keyStore;
            _trackerClient = trackerClient;
        }

        [HttpPost("generate")]
        [ProducesResponseType<KeyPairResult>(200)]
        public async Task<IResult> GenerateKeyPair()
        {
            var keyPair = await _keyPairService.GenerateKeyPairBase64Async();

            return Results.Ok(keyPair);
        }

        [HttpGet]
        [ProducesResponseType<KeyPairResult>(200)]
        public async Task<IResult> GetKeyPair()
        {
            var key = await _keyStore.GetAsync();

            return Results.Ok(key);
        }

        [HttpPost("challenge/solve")]
        public IResult SolveChallenge([FromBody] KeyChallengeRequest request)
        {
            var signature = _keyPairService.SolveChallenge(request.NonceBase64, request.PrivateKeyBase64);

            return Results.Ok(signature);
        }

        [HttpPost("challenges")]
        public async Task<IResult> RequestChallenge([FromBody] CreateChallengeRequest request)
        {
            // Proxy to Tracker
            try
            {
                var response = await _trackerClient.CreateChallengeAsync(request.PublicKey);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to request challenge from tracker: {ex.Message}");
            }
        }

        [HttpPost("challenges/verify")]
        public async Task<IResult> VerifySignature([FromBody] VerifySignatureRequest request)
        {
            // Proxy to Tracker
            try
            {
                var response = await _trackerClient.VerifyChallengeAsync(request.PublicKey, request.ChallengeId, request.SignatureBase64);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                // If verification failed with exception, it might be 400 or network error
                // We return bad request or similar
                return Results.BadRequest(new { valid = false, error = ex.Message });
            }
        }

        public class CreateChallengeRequest
        {
            public string PublicKey { get; set; } = "";
        }

        public class VerifySignatureRequest
        {
            public string ChallengeId { get; set; } = "";
            public string SignatureBase64 { get; set; } = "";
            public string PublicKey { get; set; } = "";
        }

    }
}
