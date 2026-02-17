using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>Manifest publication — announcing and authorizing chapter manifests.</summary>
    public interface IManifestAnnouncer
    {
        Task AnnounceManifestAsync(Shared.Models.AnnounceManifestRequest announcement, CancellationToken ct = default);
        Task AuthorizeManifestAsync(Shared.Models.AuthorizeManifestRequest request);
    }
}
