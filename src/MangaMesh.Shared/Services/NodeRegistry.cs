using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using System.Collections.Concurrent;

namespace MangaMesh.Shared.Services;

public class NodeRegistry : INodeRegistry
{
    private readonly ConcurrentDictionary<string, TrackerNode> _nodes = new();

    public void RegisterOrUpdate(TrackerNode node)
    {
        _nodes.AddOrUpdate(node.NodeId, node, (key, existing) =>
        {
            // Update mutable fields on existing node if we want to keep it same object reference?
            // Or just replace?
            // TrackerNode is a record.
            // If we replace, we might lose some internal state if any exists.
            // But validation logic in Controller creates a NEW node each time.
            // So we should just use the new node?
            // Actually, the original code looked like it was merging?
            /* Original:
            node.LastSeen = DateTime.UtcNow;
            return node;
        });    */
            // The controller logic now passes a node with populated ManifestDetails.
            // The text I viewed showed:
            // Manifests = existing.Manifests
            // This means if a node re-announces, we KEPT the old manifests list?
            // If the client sends a NEW list of manifests (request.Manifests), we should probably use THAT?
            // TrackerController.Announce builds a NEW node with `Manifests = request.Manifests`.
            // So we should probably overwrite.
            // However, `request.Manifests` might be partial?
            // "Check if manifest set matches" in Ping logic implies client keeps track.
            // Let's assume for now we want the LATEST state from the client.
            // But if we overwrite, we lose any server-side state.
            // For now, I will return `node` (the new one) but ensure LastSeen is updated.
            node.LastSeen = DateTime.UtcNow;
            return node;
        });

        // Current impl of RegisterOrUpdate in Controller does:
        // var node = new TrackerNode() { ... Manifests = request.Manifests ... }
        // _nodeRegistry.RegisterOrUpdate(node);
        // So the new node HAS the manifests.
        // The old code in NodeRegistry seemed to preserve existing.Manifests?
        // That seems wrong if the intention is to update the set.
        // Unless Announce is additive?
        // AnnounceRequest has all hashes.
        // So replacing is correct.
    }

    public IEnumerable<TrackerNode> GetAllNodes()
    {
        return _nodes.Values;
    }

    public TrackerNode? GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    public List<TrackerNode> GetPeersForManifest(string hash)
    {
        return _nodes.Values
            .Where(n => n.Manifests.Contains(hash))
            .ToList();
    }

    public void AddManifestToNode(string nodeId, string manifestHash)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.Manifests.Add(manifestHash);
            node.LastSeen = DateTime.UtcNow;
        }
    }

    public int GetNodeCount()
    {
        return _nodes.Count;
    }
}
