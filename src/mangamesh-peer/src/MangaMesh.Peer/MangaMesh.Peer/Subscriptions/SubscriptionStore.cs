using MangaMesh.Client.Series;
using MangaMesh.Shared.Stores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaMesh.Client.Subscriptions
{
    public class SubscriptionStore : ISubscriptionStore
    {
        private readonly string _path;

        public SubscriptionStore(string rootPath)
        {
            _path = Path.Combine(rootPath, "subscriptions.json");
        }

        public async Task<IReadOnlyList<SeriesSubscription>> GetAllAsync(CancellationToken ct = default)
        {
            return (await LoadAsync()).AsReadOnly();
        }

        public async Task AddAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            var subs = await LoadAsync();
            subs.RemoveAll(s => s.SeriesId == subscription.SeriesId);
            subs.Add(subscription);
            await SaveAsync(subs);
        }

        public async Task RemoveAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            var subs = await LoadAsync();
            subs.RemoveAll(s => s.SeriesId == subscription.SeriesId);
            await SaveAsync(subs);
        }

        public async Task<bool> ExistsAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            var subs = await LoadAsync();
            return subs.Any(s => s.SeriesId == subscription.SeriesId);
        }

        private async Task<List<SeriesSubscription>> LoadAsync()
        {
            return (await JsonFileStore.LoadAsync<SeriesSubscription>(_path)).ToList();
        }

        private async Task SaveAsync(List<SeriesSubscription> subs)
        {
            await JsonFileStore.SaveAsync(_path, subs);
        }
    }
}
