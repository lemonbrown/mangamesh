namespace MangaMesh.Peer.Core.Configuration
{
    public class DhtOptions
    {
        public string BootstrapNodesPath { get; set; } = "config/bootstrap_nodes.yml";
        public int MessageTimeoutMs { get; set; } = 2000;
        public int MaxMessageSizeBytes { get; set; } = 10_485_760; // 10 MB
        public int ChunkSizeBytes { get; set; } = 262_144; // 256 KB
    }
}
