using MangaMesh.Peer.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Peer.Core.Keys
{
    public class SqliteKeyStore : IKeyStore
    {
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public SqliteKeyStore(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            // check config first
            var pubKey = _configuration["Node:PublicKey"];
            var privKey = _configuration["Node:PrivateKey"];

            if (!string.IsNullOrEmpty(pubKey) && !string.IsNullOrEmpty(privKey))
            {
                return new PublicPrivateKeyPair(pubKey, privKey);
            }

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
