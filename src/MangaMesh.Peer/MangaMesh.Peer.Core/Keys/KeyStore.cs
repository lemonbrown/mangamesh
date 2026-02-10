using MangaMesh.Shared.Stores;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyStore : IKeyStore
    {
        public async Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {

            var key = new PublicPrivateKeyPair(publicKeyBase64, privateKeyBase64);

            //var key = new PublicKey(publicKeyBase64);

            await JsonFileStore.SaveAsync(AppContext.BaseDirectory + "\\data\\keys\\keys.json", key);

            //await JsonFileStore.SaveAsync(AppContext.BaseDirectory + "\\data\\keys\\public_key.json", key);            
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            //var key = await JsonFileStore.LoadSingleAsync<PublicKey>(AppContext.BaseDirectory + "\\data\\keys\\public_key.json");

            var key = await JsonFileStore.LoadSingleAsync<PublicPrivateKeyPair>(AppContext.BaseDirectory + "\\data\\keys\\keys.json");

            return key;
        }
    }
}
