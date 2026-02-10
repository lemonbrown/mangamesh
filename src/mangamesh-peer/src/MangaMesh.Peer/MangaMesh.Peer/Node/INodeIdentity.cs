using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Node
{
    public interface INodeIdentity
    {
        byte[] NodeId { get; }                // SHA-256 of public key
        byte[] PublicKey { get; }
        byte[] Sign(byte[] data);
        bool Verify(byte[] data, byte[] signature);
    }
}
