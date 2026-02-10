using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Helpers
{

    public static class Crypto
    {
        public static byte[] Sha256(byte[] data) => System.Security.Cryptography.SHA256.HashData(data);
        /// <summary>
        /// Signs data using Ed25519 with the given private key.
        /// </summary>
        /// <param name="privateKey">32-byte private key (seed) or 64-byte expanded key.</param>
        /// <param name="data">Data to sign.</param>
        /// <returns>Signature (64 bytes).</returns>
        //public static byte[] Ed25519Sign(byte[] privateKey, byte[] data)
        //{
        //    if (privateKey.Length != 32)
        //        throw new ArgumentException("Ed25519 private key must be 32 bytes (seed).");

        //    // Create an Ed25519 key pair from the private key seed
        //    using var key = ECDsa.Create(new ECParameters
        //    {
        //        Curve = ECCurve.CreateFromFriendlyName("ed25519"),
        //        D = privateKey
        //    });

        //    return key.SignData(data, HashAlgorithmName.SHA512); // Ed25519 uses SHA-512 internally
        //}

        public static byte[] Ed25519Sign(byte[] privateKey, byte[] data)
        {
            if (privateKey.Length != 32)
                throw new ArgumentException("Ed25519 private key must be 32 bytes (seed).");

            // privateKey must be 32 bytes (seed)
            var key = Key.Import(SignatureAlgorithm.Ed25519, privateKey, KeyBlobFormat.RawPrivateKey);
            return SignatureAlgorithm.Ed25519.Sign(key, data);
        }

        public static bool Ed25519Verify(byte[] publicKey, byte[] data, byte[] signature) => throw new NotImplementedException();

        public static byte[] Hash(params object[] items)
        {
            // simplistic hash combination
            using var sha = System.Security.Cryptography.SHA256.Create();
            foreach (var item in items)
            {
                if (item is byte[] b)
                    sha.TransformBlock(b, 0, b.Length, null, 0);
                else if (item is string s)
                    sha.TransformBlock(System.Text.Encoding.UTF8.GetBytes(s), 0, s.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash!;
        }

        public static BigInteger XorDistance(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Length mismatch");
            var result = new byte[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = (byte)(a[i] ^ b[i]);
            return new BigInteger(result, isUnsigned: true, isBigEndian: true);
        }

        /// <summary>
        /// Generates a cryptographically secure 256-bit random node ID.
        /// </summary>
        public static byte[] RandomNodeId()
        {
            byte[] nodeId = new byte[32]; // 256 bits
            RandomNumberGenerator.Fill(nodeId);
            return nodeId;
        }
    }
}
