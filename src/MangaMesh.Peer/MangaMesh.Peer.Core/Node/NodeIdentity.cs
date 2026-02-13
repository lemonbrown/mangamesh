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

        public NodeIdentity(IKeyPairService keyPairService, Microsoft.Extensions.Configuration.IConfiguration configuration, IKeyStore keyStore)
        {
            string? pubKeyBase64 = configuration["Node:PublicKey"];
            string? privKeyBase64 = configuration["Node:PrivateKey"];

            if (!string.IsNullOrEmpty(pubKeyBase64) && !string.IsNullOrEmpty(privKeyBase64))
            {
                // 1. Use Configured Keys
                Console.WriteLine($"[NodeIdentity] Loaded Identity from Configuration: {pubKeyBase64.Substring(0, 10)}...");
                PublicKey = Convert.FromBase64String(pubKeyBase64);
                _privateKey = Convert.FromBase64String(privKeyBase64);
            }
            else
            {
                // 2. Try Load from KeyStore
                var storedKeys = keyStore.GetAsync().Result;
                if (storedKeys != null)
                {
                    Console.WriteLine($"[NodeIdentity] Loaded Identity from KeyStore: {storedKeys.PublicKeyBase64.Substring(0, 10)}...");
                    PublicKey = Convert.FromBase64String(storedKeys.PublicKeyBase64);
                    _privateKey = Convert.FromBase64String(storedKeys.PrivateKeyBase64);
                }
                else
                {
                    // 3. Generate New Keys
                    Console.WriteLine("[NodeIdentity] No Identity found. Generating new KeyPair...");
                    var result = keyPairService.GenerateKeyPairBase64Async().Result;

                    PublicKey = Convert.FromBase64String(result.PublicKeyBase64);
                    _privateKey = Convert.FromBase64String(result.PrivateKeyBase64);
                }
            }

            NodeId = Crypto.Sha256(PublicKey);
            Console.WriteLine($"[NodeIdentity] Active NodeID: {Convert.ToHexString(NodeId).ToLower()}");
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
