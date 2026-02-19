using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using System.Collections.Concurrent;

namespace MangaMesh.Shared.Services;

public class NodeRegistry : INodeRegistry
{
    private readonly ConcurrentDictionary<string, TrackerNode> _nodes = new();

    public void RegisterOrUpdate(TrackerNode node)
    {
        node.LastSeen = DateTime.UtcNow;
        _nodes.AddOrUpdate(node.NodeId, node, (key, existing) => node);
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
