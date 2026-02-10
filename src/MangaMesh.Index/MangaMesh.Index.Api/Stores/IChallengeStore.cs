using MangaMesh.Index.Api.Models;

namespace MangaMesh.Index.Api.Stores
{
    public interface IChallengeStore
    {
        Task StoreAsync(KeyChallenge challenge);

        Task<KeyChallenge?> GetAsync(string challengeId);

        Task DeleteAsync(string challengeId);

        Task CleanupExpiredAsync();
    }

}
