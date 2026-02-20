namespace MangaMesh.Shared.Stores
{
    public record ManifestAnnouncer(string ManifestHash, string NodeId, DateTime AnnouncedAt);

    public interface IManifestAnnouncerStore
    {
        /// <summary>Records that a node announced a manifest. Silently ignored if already recorded.</summary>
        Task RecordAsync(string manifestHash, string nodeId, DateTime announcedAt);

        Task<IEnumerable<ManifestAnnouncer>> GetByManifestHashAsync(string manifestHash);

        Task DeleteByManifestHashAsync(string manifestHash);
    }
}
