using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface IApprovedKeyStore
    {
        Task<bool> IsKeyApprovedAsync(string publicKeyBase64);
        Task ApproveKeyAsync(string publicKeyBase64, string comment);
        Task RevokeKeyAsync(string publicKeyBase64);
        Task<IEnumerable<ApprovedKeyRecord>> GetAllApprovedAsync();
    }
}
