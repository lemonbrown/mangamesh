using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface IChallengeStore
    {
        Task StoreAsync(KeyChallenge challenge);

        Task<KeyChallenge?> GetAsync(string challengeId);

        Task DeleteAsync(string challengeId);

        Task CleanupExpiredAsync();
    }

}
