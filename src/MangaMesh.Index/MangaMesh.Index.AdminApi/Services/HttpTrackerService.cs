using MangaMesh.Index.AdminApi.Configuration;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MangaMesh.Index.AdminApi.Services;

public class HttpTrackerService : ITrackerService
{
    private readonly HttpClient _httpClient;
    private readonly TrackerSettings _settings;
    private readonly ILogger<HttpTrackerService> _logger;

    public HttpTrackerService(HttpClient httpClient, IOptions<TrackerSettings> settings, ILogger<HttpTrackerService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<TrackerNode>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(new Uri(_settings.Url), "/tracker/nodes");
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var nodes = JsonSerializer.Deserialize<IEnumerable<TrackerNode>>(json, options);

            return nodes ?? Enumerable.Empty<TrackerNode>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch nodes from Tracker at {Url}", _settings.Url);
            // Returning empty list for now to avoid crashing the dashboard, 
            // but in a real app might want to propagate or return error model.
            return Enumerable.Empty<TrackerNode>();
        }
    }
}
