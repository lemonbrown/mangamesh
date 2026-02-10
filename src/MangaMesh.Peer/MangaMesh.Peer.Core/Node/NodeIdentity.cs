using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Keys;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    // Simple Ed25519 identity implementation placeholder
    public class NodeIdentity : INodeIdentity
    {
        public byte[] NodeId { get; private set; }
        public byte[] PublicKey { get; private set; }
        private byte[] _privateKey;

        public NodeIdentity(IKeyPairService keyPairService)
        {
            var result = keyPairService.GenerateKeyPairBase64Async().Result;

            PublicKey = Convert.FromBase64String(result.PublicKeyBase64);
            _privateKey = Convert.FromBase64String(result.PrivateKeyBase64);
            NodeId = Crypto.Sha256(PublicKey);
        }

        public byte[] Sign(byte[] data)
        {
            return Crypto.Ed25519Sign(_privateKey, data);
        }

        public bool Verify(byte[] data, byte[] signature)
        {
            return Crypto.Ed25519Verify(PublicKey, data, signature);
        }
    }

}
