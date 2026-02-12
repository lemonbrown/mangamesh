using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Data; // For SqliteKeyStore
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class SingletonKeyStore : IKeyStore
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SingletonKeyStore(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<SqliteKeyStore>();
                return await store.GetAsync();
            }
        }

        public async Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<SqliteKeyStore>();
                await store.SaveAsync(publicKeyBase64, privateKeyBase64);
            }
        }
    }
}
