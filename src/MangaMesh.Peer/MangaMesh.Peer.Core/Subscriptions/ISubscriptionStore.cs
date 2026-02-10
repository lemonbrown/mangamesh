using MangaMesh.Peer.Core.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Subscriptions
{
    public interface ISubscriptionStore
    {
        Task<IReadOnlyList<SeriesSubscription>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(SeriesSubscription subscription, CancellationToken cancellationToken = default);
        Task RemoveAsync(SeriesSubscription releaseLine, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(SeriesSubscription releaseLine, CancellationToken cancellationToken = default);
    }


}
