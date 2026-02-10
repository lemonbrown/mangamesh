using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Keys
{
    public class PublicPrivateKeyPair
    {
        public string PublicKeyBase64 { get; set; } = string.Empty;
        public string PrivateKeyBase64 { get; set; } = string.Empty;

        public PublicPrivateKeyPair() { }

        public PublicPrivateKeyPair(string publicKeyBase64, string privateKeyBase64)
        {
            PublicKeyBase64 = publicKeyBase64;
            PrivateKeyBase64 = privateKeyBase64;
        }
    }
}
