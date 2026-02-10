using MangaMesh.Peer.Core.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Metadata
{
    public sealed class HttpMetadataClient : IMetadataClient
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public HttpMetadataClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string seriesId,
            string language,
            CancellationToken ct = default)
        {
            var url =
                $"/api/series/" +
                $"{seriesId}/" +
                $"{language}/chapters";

            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var chapters = JsonSerializer.Deserialize<List<ChapterMetadata>>(json, JsonOptions);

            return chapters ?? new List<ChapterMetadata>();
        }

    }

}
