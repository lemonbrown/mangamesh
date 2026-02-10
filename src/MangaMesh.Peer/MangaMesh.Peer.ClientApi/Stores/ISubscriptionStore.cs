using MangaMesh.Peer.ClientApi.Models;

namespace MangaMesh.Peer.ClientApi.Stores
{
    public interface ISubscriptionStore
    {
        /// <summary>
        /// Get all subscribed release lines.
        /// </summary>
        Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Add a new subscription.
        /// Returns true if added, false if it already exists.
        /// </summary>
        Task<bool> AddAsync(SubscriptionDto subscription, CancellationToken ct = default);

        /// <summary>
        /// Remove a subscription.
        /// Returns true if removed, false if it did not exist.
        /// </summary>
        Task<bool> RemoveAsync(SubscriptionDto subscription, CancellationToken ct = default);
    }

}
