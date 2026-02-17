using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Shared.Stores;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyStore : IKeyStore
    {
        private readonly string _keyFilePath;

        public KeyStore(IOptions<KeyStoreOptions> options)
        {
            _keyFilePath = options.Value.KeyFilePath;
        }

        public async Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {
            var key = new PublicPrivateKeyPair(publicKeyBase64, privateKeyBase64);
            await JsonFileStore.SaveAsync(_keyFilePath, key);
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            return await JsonFileStore.LoadSingleAsync<PublicPrivateKeyPair>(_keyFilePath);
        }
    }
}
