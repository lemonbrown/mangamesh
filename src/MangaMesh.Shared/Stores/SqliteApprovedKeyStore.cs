using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteApprovedKeyStore : IApprovedKeyStore
    {
        private readonly IndexDbContext _db;

        public SqliteApprovedKeyStore(IndexDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IsKeyApprovedAsync(string publicKeyBase64)
        {
            return await _db.ApprovedKeys.AnyAsync(k => k.PublicKeyBase64 == publicKeyBase64);
        }

        public async Task ApproveKeyAsync(string publicKeyBase64, string comment)
        {
            if (!await IsKeyApprovedAsync(publicKeyBase64))
            {
                _db.ApprovedKeys.Add(new IndexApprovedKeyEntity
                {
                    PublicKeyBase64 = publicKeyBase64,
                    Comment = comment,
                    AddedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeKeyAsync(string publicKeyBase64)
        {
            var key = await _db.ApprovedKeys.FirstOrDefaultAsync(k => k.PublicKeyBase64 == publicKeyBase64);
            if (key != null)
            {
                _db.ApprovedKeys.Remove(key);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ApprovedKeyRecord>> GetAllApprovedAsync()
        {
            return await _db.ApprovedKeys
                .Select(k => new ApprovedKeyRecord
                {
                    PublicKeyBase64 = k.PublicKeyBase64,
                    Comment = k.Comment,
                    AddedAt = k.AddedAt
                })
                .ToListAsync();
        }
    }
}
