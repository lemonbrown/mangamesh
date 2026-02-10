using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtStorage
    {
        void StoreContent(byte[] contentHash, byte[] publisherNodeId);
        List<byte[]> GetNodesForContent(byte[] contentHash);
        List<byte[]> GetAllContentHashes();
    }
}
