using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class KBucket
    {
        public List<RoutingEntry> Entries { get; private set; } = new();
        public int MaxSize { get; private set; } = 20;

        public void AddOrUpdate(RoutingEntry entry)
        {
            var existing = Entries.Find(e => e.NodeId.AsSpan().SequenceEqual(entry.NodeId));
            if (existing != null)
            {
                existing.LastSeenUtc = DateTime.UtcNow;
                existing.Address = entry.Address;
            }
            else
            {
                if (Entries.Count >= MaxSize)
                    Entries.RemoveAt(0); // simple eviction, can improve with LRU
                Entries.Add(entry);
            }
        }
    }
}
