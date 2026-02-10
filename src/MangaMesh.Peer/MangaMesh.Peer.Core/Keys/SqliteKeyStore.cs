using MangaMesh.Peer.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Peer.Core.Keys
{
    public class SqliteKeyStore : IKeyStore
    {
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

        public SqliteKeyStore(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
                var entity = await context.Keys.OrderByDescending(k => k.CreatedAt).FirstOrDefaultAsync();

                if (entity == null)
                {
                    return null;
                }

                return new PublicPrivateKeyPair(entity.PublicKey, entity.PrivateKey);
            }
        }

        public async Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
                var entity = new KeyEntity
                {
                    PublicKey = publicKeyBase64,
                    PrivateKey = privateKeyBase64,
                    CreatedAt = DateTime.UtcNow
                };

                context.Keys.Add(entity);
                await context.SaveChangesAsync();
            }
        }
    }
}
