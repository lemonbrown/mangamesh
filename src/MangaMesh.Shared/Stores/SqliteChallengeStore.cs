using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteChallengeStore : IChallengeStore
    {
        private readonly IndexDbContext _context;

        public SqliteChallengeStore(IndexDbContext context)
        {
            _context = context;
        }

        public async Task StoreAsync(KeyChallenge challenge)
        {
            var entity = new IndexChallengeEntity
            {
                Id = challenge.Id,
                UserId = challenge.UserId,
                Nonce = challenge.Nonce,
                ExpiresAt = challenge.ExpiresAt
            };

            // Should verify unique ID?
            // Assuming ID is unique.
            _context.Challenges.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<KeyChallenge?> GetAsync(string challengeId)
        {
            var entity = await _context.Challenges.FindAsync(challengeId);
            if (entity == null) return null;

            return new KeyChallenge
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Nonce = entity.Nonce,
                ExpiresAt = entity.ExpiresAt
            };
        }

        public async Task DeleteAsync(string challengeId)
        {
            var entity = await _context.Challenges.FindAsync(challengeId);
            if (entity != null)
            {
                _context.Challenges.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task CleanupExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _context.Challenges.Where(c => c.ExpiresAt <= now).ToListAsync();

            if (expired.Any())
            {
                _context.Challenges.RemoveRange(expired);
                await _context.SaveChangesAsync();
            }
        }
    }
}
