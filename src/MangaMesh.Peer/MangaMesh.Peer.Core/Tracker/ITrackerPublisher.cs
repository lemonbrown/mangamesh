namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>
    /// Encapsulates the full challenge-response auth + manifest publication flow.
    /// Callers build the <see cref="Shared.Models.AnnounceManifestRequest"/> and hand it off here.
    /// </summary>
    public interface ITrackerPublisher
    {
        Task PublishManifestAsync(Shared.Models.AnnounceManifestRequest request, CancellationToken ct = default);
    }
}
