using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface IPublicKeyStore
    {
        Task StoreAsync(PublicKeyRecord record);

        Task<PublicKeyRecord?> GetByUserIdAsync(string userId);

        Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64);

        Task<PublicKeyRecord?> GetByKeyIdAsync(string publicKeyId);

        Task RevokeAsync(string publicKeyId);

        Task<IEnumerable<PublicKeyRecord>> GetAllAsync();
    }

}
