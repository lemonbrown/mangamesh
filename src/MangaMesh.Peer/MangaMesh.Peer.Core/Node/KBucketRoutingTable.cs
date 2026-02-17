using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public sealed class KBucketRoutingTable : IRoutingTable
    {
        private readonly List<KBucket> _buckets;
        private readonly byte[] _localNodeId;

        public int BucketCount => _buckets.Count;

        public KBucketRoutingTable(byte[] localNodeId, int bucketCount = 256)
        {
            _localNodeId = localNodeId;
            _buckets = new List<KBucket>(bucketCount);
            for (int i = 0; i < bucketCount; i++)
                _buckets.Add(new KBucket());
        }

        public void AddOrUpdate(RoutingEntry entry)
        {
            if (entry.NodeId == null || entry.NodeId.Length == 0) return;
            int index = GetBucketIndex(entry.NodeId);
            _buckets[index].AddOrUpdate(entry);
        }

        public IReadOnlyList<RoutingEntry> FindClosest(byte[] targetId, int k = 20)
        {
            var all = GetAll() as List<RoutingEntry> ?? GetAll().ToList();
            all.Sort((a, b) =>
            {
                var distA = Crypto.XorDistance(a.NodeId, targetId);
                var distB = Crypto.XorDistance(b.NodeId, targetId);
                return distA.CompareTo(distB);
            });
            return all.GetRange(0, Math.Min(k, all.Count));
        }

        public IReadOnlyList<RoutingEntry> GetAll()
        {
            var all = new List<RoutingEntry>();
            foreach (var bucket in _buckets)
                all.AddRange(bucket.Entries);
            return all;
        }

        public NodeAddress? GetAddressForNode(byte[] nodeId)
        {
            foreach (var bucket in _buckets)
            {
                var entry = bucket.Entries.Find(e => e.NodeId.AsSpan().SequenceEqual(nodeId));
                if (entry != null) return entry.Address;
            }
            return null;
        }

        private int GetBucketIndex(byte[] nodeId)
        {
            var distance = Crypto.XorDistance(_localNodeId, nodeId);
            return (int)Math.Min(_buckets.Count - 1, distance.BitLength() - 1);
        }
    }
}
