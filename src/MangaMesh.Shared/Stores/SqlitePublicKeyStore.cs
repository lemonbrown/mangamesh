using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqlitePublicKeyStore : IPublicKeyStore
    {
        private readonly IndexDbContext _context;

        public SqlitePublicKeyStore(IndexDbContext context)
        {
            _context = context;
        }

        public async Task StoreAsync(PublicKeyRecord record)
        {
            var exists = await _context.Keys.AnyAsync(k => k.PublicKeyBase64 == record.PublicKeyBase64);
            if (!exists)
            {
                _context.Keys.Add(new IndexKeyEntity
                {
                    PublicKeyBase64 = record.PublicKeyBase64,
                    RegisteredAt = record.RegisteredAt,
                    Revoked = record.Revoked
                });
                await _context.SaveChangesAsync();
            }
        }

        public async Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64)
        {
            // Handle URL-encoded keys just in case, similar to Json store logic
            var decoded = Uri.UnescapeDataString(publicKeyBase64);

            var entity = await _context.Keys.FindAsync(decoded);
            if (entity == null) return null;

            return new PublicKeyRecord
            {
                PublicKeyBase64 = entity.PublicKeyBase64,
                RegisteredAt = entity.RegisteredAt,
                Revoked = entity.Revoked
            };
        }

        public async Task RevokeAsync(string publicKeyId)
        {
            // Assuming publicKeyId is the publicKeyBase64 in this context
            var entity = await _context.Keys.FindAsync(publicKeyId);
            if (entity != null)
            {
                entity.Revoked = true;
                await _context.SaveChangesAsync();
            }
        }

        // Methods not supported by current model/schema
        public Task<PublicKeyRecord?> GetByUserIdAsync(string userId) => Task.FromResult<PublicKeyRecord?>(null);
        public Task<PublicKeyRecord?> GetByKeyIdAsync(string publicKeyId) => Task.FromResult<PublicKeyRecord?>(null);

        public async Task<IEnumerable<PublicKeyRecord>> GetAllAsync()
        {
            return await _context.Keys
                .Select(k => new PublicKeyRecord
                {
                    PublicKeyBase64 = k.PublicKeyBase64,
                    RegisteredAt = k.RegisteredAt,
                    Revoked = k.Revoked
                })
                .ToListAsync();
        }
    }
}
