using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Services
{
    public interface IPublicKeyRegistry
    {
        //Task RegisterKeyAsync(PublicKeyRecord record);

        Task<KeyChallengeResponse> CreateChallengeAsync(string userId);

        Task<KeyVerificationResponse> VerifyChallengeAsync(
            string challengeId,
            byte[] signature);
    }

}
