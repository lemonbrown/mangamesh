using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Keys
{
    public interface IKeyStore
    {
        Task SaveAsync(string publicKeyBase64, string privateKeyBase64);
        public Task<PublicPrivateKeyPair?> GetAsync();
    }
}
