using MangaMesh.Shared.Models;

namespace MangaMesh.Index.AdminApi.Services;

public interface ITrackerService
{
    Task<IEnumerable<TrackerNode>> GetAllNodesAsync(CancellationToken cancellationToken = default);
}
