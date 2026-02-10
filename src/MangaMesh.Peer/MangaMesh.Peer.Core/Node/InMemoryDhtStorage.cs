using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{

    // Simple in-memory storage
    public class InMemoryDhtStorage : IDhtStorage
    {
        private readonly Dictionary<string, List<byte[]>> _store = new();

        public void StoreContent(byte[] contentHash, byte[] publisherNodeId)
        {
            var key = Convert.ToBase64String(contentHash);
            if (!_store.ContainsKey(key))
                _store[key] = new List<byte[]>();
            if (!_store[key].Contains(publisherNodeId))
                _store[key].Add(publisherNodeId);
        }

        public List<byte[]> GetNodesForContent(byte[] contentHash)
        {
            var key = Convert.ToBase64String(contentHash);
            if (_store.TryGetValue(key, out var nodes))
                return nodes;
            return new List<byte[]>();
        }

        public List<byte[]> GetAllContentHashes()
        {
            return _store.Values.SelectMany(x => x).Distinct().ToList();
        }
    }
}
