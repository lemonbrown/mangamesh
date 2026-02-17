namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>
    /// Convenience aggregator that combines all focused tracker interfaces.
    /// Use the narrower interfaces (IPeerLocator, INodeAnnouncer, etc.) wherever possible.
    /// </summary>
    public interface ITrackerClient
        : IPeerLocator
        , INodeAnnouncer
        , ISeriesRegistry
        , IManifestAnnouncer
        , ITrackerChallengeClient
    {
    }

    public class TrackerStats
    {
        public int NodeCount { get; set; }
    }
}
