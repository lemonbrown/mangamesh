namespace MangaMesh.Peer.Core.Configuration
{
    public class BlobStoreOptions
    {
        public string RootPath { get; set; } = "data/blobs";
        public long MaxStorageBytes { get; set; } = 5L * 1024 * 1024 * 1024; // 5 GB
    }
}
