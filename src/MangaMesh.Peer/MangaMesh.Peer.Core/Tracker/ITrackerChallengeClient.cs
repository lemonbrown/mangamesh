using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>Challenge-response authentication flow for tracker operations.</summary>
    public interface ITrackerChallengeClient
    {
        Task<KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64);
        Task<KeyVerificationResponse> VerifyChallengeAsync(string publicKeyBase64, string challengeId, string signatureBase64);
    }
}
