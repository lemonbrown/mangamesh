using MangaMesh.Peer.Core.Content;

namespace MangaMesh.Peer.GatewayApi.Services;

public interface IGatewayCache
{
    Task<ManifestData?> GetManifestAsync(string hash);
    Task PutManifestAsync(ManifestData manifest);

    Task<byte[]?> GetBlobAsync(string hash);
    Task PutBlobAsync(string hash, byte[] data);
}
