using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;

namespace MangaMesh.Shared.Stores
{
    public class JsonKeyStore : IPublicKeyStore
    {
        private readonly string _filePath;
        private readonly string _fileName;

        public JsonKeyStore(string filePath, string filename)
        {
            Directory.CreateDirectory(filePath);

            if (!File.Exists(filePath + "\\" + filename))
            {
                File.Create(filePath + "\\" + filename);
            }

            _filePath = filePath;
            _fileName = filename;
        }

        public async Task StoreAsync(PublicKeyRecord record)
        {
            var keys = await JsonFileStore.LoadAsync<PublicKeyRecord>(_filePath + "\\" + _fileName);

            // Remove existing entry if present
            keys.RemoveAll(k => k.PublicKeyBase64 == record.PublicKeyBase64);

            keys.Add(record);

            await JsonFileStore.SaveAsync(_filePath + "\\" + _fileName, keys);
        }

        public async Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64)
        {
            var keys = await JsonFileStore.LoadAsync<PublicKeyRecord>(_filePath + "\\" + _fileName);
            // Search by decoding the stored key, just in case it was saved URL-encoded
            var match = keys.FirstOrDefault(k => Uri.UnescapeDataString(k.PublicKeyBase64) == publicKeyBase64);

            // If found, ensure we return the clean, decoded Base64 string
            if (match != null)
            {
                match.PublicKeyBase64 = Uri.UnescapeDataString(match.PublicKeyBase64);
            }
            return match;
        }

        public async Task<IEnumerable<PublicKeyRecord>> GetAllAsync()
        {
            return await JsonFileStore.LoadAsync<PublicKeyRecord>(_filePath + "\\" + _fileName);
        }

        public Task<PublicKeyRecord?> GetByUserIdAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<PublicKeyRecord?> GetByKeyIdAsync(string publicKeyId)
        {
            throw new NotImplementedException();
        }

        public Task RevokeAsync(string publicKeyId)
        {
            throw new NotImplementedException();
        }
    }

}
