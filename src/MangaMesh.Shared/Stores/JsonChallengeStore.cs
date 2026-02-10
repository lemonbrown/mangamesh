using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;

namespace MangaMesh.Shared.Stores
{
    public class JsonChallengeStore : IChallengeStore
    {
        private readonly string _filePath;
        private readonly string _filename;

        public JsonChallengeStore(string filePath, string filename)
        {
            Directory.CreateDirectory(filePath);

            if (!File.Exists(filePath + "\\" + filename))
            {
                File.Create(filePath + "\\" + filename);
            }

            _filePath = filePath;
            _filename = filename;
        }

        public async Task StoreAsync(KeyChallenge challenge)
        {
            var challenges = await JsonFileStore.LoadAsync<KeyChallenge>(_filePath + "\\" + _filename);

            challenges.RemoveAll(c => c.Id == challenge.Id);
            challenges.Add(challenge);

            await JsonFileStore.SaveAsync(_filePath + "\\" + _filename, challenges);
        }

        public async Task<KeyChallenge?> GetAsync(string challengeId)
        {
            var challenges = await JsonFileStore.LoadAsync<KeyChallenge>(_filePath + "\\" + _filename);

            return challenges.FirstOrDefault(c => c.Id == challengeId);
        }

        public async Task DeleteAsync(string challengeId)
        {
            var challenges = await JsonFileStore.LoadAsync<KeyChallenge>(_filePath + "\\" + _filename);

            var removed = challenges.RemoveAll(c => c.Id == challengeId) > 0;
            if (removed)
                await JsonFileStore.SaveAsync(_filePath + "\\" + _filename, challenges);
        }

        public async Task CleanupExpiredAsync()
        {
            var challenges = await JsonFileStore.LoadAsync<KeyChallenge>(_filePath + "\\" + _filename);

            var now = DateTime.UtcNow;
            var removed = challenges.RemoveAll(c => c.ExpiresAt <= now) > 0;

            if (removed)
                await JsonFileStore.SaveAsync(_filePath + "\\" + _filename, challenges);
        }
    }


}
