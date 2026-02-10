using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Services;

public interface INodeRegistry
{
    void RegisterOrUpdate(TrackerNode node);
    TrackerNode? GetNode(string nodeId);
    List<TrackerNode> GetPeersForManifest(string hash);
    void AddManifestToNode(string nodeId, string manifestHash);
    int GetNodeCount();
    IEnumerable<TrackerNode> GetAllNodes();
}
