using MangaMesh.Shared.Models;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using MangaMesh.Shared.Models.MangaDex;

namespace MangaMesh.Shared.Services
{
    public class MangaDexMetadataProvider : IMangaMetadataProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _username;
        private readonly string _password;
        private string? _sessionToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public MangaDexMetadataProvider(HttpClient httpClient, string username, string password)
        {
            _httpClient = httpClient;
            _username = username;
            _password = password;

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://api.mangadex.org");
            }
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "MangaMesh/1.0 (mangamesh@example.com)");
            }
        }

        private async Task EnsureAuthenticatedAsync()
        {
            if (!string.IsNullOrEmpty(_sessionToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest
                {
                    Username = _username,
                    Password = _password
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>(); // This might wrap inside 'token' property based on model?
                                                                                            // Wait, LoginResponse model: { token: { session: "..." } } matches likely structure?
                                                                                            // Actually standard response is { result: "ok", token: { session: "", refresh: "" } }
                                                                                            // My LoginResponse class defined earlier: 
                                                                                            // public class LoginResponse { [JsonPropertyName("token")] public TokenData Token { get; set; } }
                                                                                            // But wait, standard response wraps in `result`? 
                                                                                            // Let's check docs or assume standard wrapper not present for auth?
                                                                                            // Docs: POST /auth/login -> 200 OK -> { "result": "ok", "token": { "session": "...", "refresh": "..." } }
                                                                                            // My LoginResponse definition assumes root object has 'token'. 
                                                                                            // However, `MangaDexResponse<T>` has `Result`, `Response`, `Data`.
                                                                                            // Auth might be different. 
                                                                                            // I will read as JsonElement first to be safe or use `LoginResponse` directly if I trust my model.
                                                                                            // My `LoginResponse` matches the structure `{ token: ... }`. It ignores `result` field unless I add it.

                    if (result?.Token != null)
                    {
                        _sessionToken = result.Token.Session;
                        _tokenExpiry = DateTime.UtcNow.AddMinutes(14); // Tokens last 15 mins usually
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sessionToken);
                    }
                }
                else
                {
                    // Log failure?
                    Console.WriteLine($"MangaDex Login Failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MangaDex Auth Error: {ex.Message}");
            }
        }

        public async Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(string query, int limit = 10)
        {
            await EnsureAuthenticatedAsync();

            try
            {
                // GET /manga?title={query}&limit={limit}
                var response = await _httpClient.GetFromJsonAsync<MangaDexResponse<List<MangaData>>>($"manga?title={Uri.EscapeDataString(query)}&limit={limit}&includes[]=cover_art");

                if (response?.Data == null) return Array.Empty<MangaSearchResult>();

                return response.Data.Select(m => new MangaSearchResult
                {
                    Source = ExternalMetadataSource.MangaDex,
                    ExternalMangaId = m.Id,
                    Title = m.Attributes.Title.Values.FirstOrDefault() ?? "Unknown Title",
                    AltTitles = m.Attributes.AltTitles.SelectMany(d => d.Values).ToList(),
                    Status = m.Attributes.Status,
                    Year = m.Attributes.Year
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search Failed: {ex.Message}");
                return Array.Empty<MangaSearchResult>();
            }
        }

        public async Task<MangaMetadata?> GetMangaAsync(string externalMangaId)
        {
            await EnsureAuthenticatedAsync();
            try
            {
                var response = await _httpClient.GetFromJsonAsync<MangaDexResponse<MangaData>>($"manga/{externalMangaId}");
                if (response?.Data == null) return null;

                var m = response.Data;
                return new MangaMetadata
                {
                    Source = ExternalMetadataSource.MangaDex,
                    ExternalMangaId = m.Id,
                    CanonicalTitle = m.Attributes.Title.Values.FirstOrDefault() ?? "Unknown Title",
                    AltTitles = m.Attributes.AltTitles.SelectMany(d => d.Values).ToList(),
                    Status = m.Attributes.Status,
                    Description = m.Attributes.Description.Values.FirstOrDefault(),
                    // Year = m.Attributes.Year // Not in MangaMetadata
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(string externalMangaId, string language)
        {
            await EnsureAuthenticatedAsync();
            try
            {
                // GET /manga/{id}/feed?translatedLanguage[]={language}&limit=500&order[chapter]=desc
                var url = $"manga/{externalMangaId}/feed?translatedLanguage[]={language}&limit=500&order[chapter]=desc&includes[]=scanlation_group";
                var response = await _httpClient.GetFromJsonAsync<MangaDexResponse<List<ChapterData>>>(url);

                if (response?.Data == null) return Array.Empty<ChapterMetadata>();

                return response.Data.Select(c => new ChapterMetadata
                {
                    Source = ExternalMetadataSource.MangaDex,
                    ExternalMangaId = externalMangaId,
                    ExternalChapterId = c.Id,
                    Language = language,
                    ChapterNumber = c.Attributes.Chapter ?? "0",
                    Volume = c.Attributes.Volume,
                    Title = c.Attributes.Title,
                    PublishDate = c.Attributes.PublishAt
                }).ToList();
            }
            catch
            {
                return Array.Empty<ChapterMetadata>();
            }
        }

        public async Task<ChapterMetadata?> GetChapterAsync(string externalMangaId, double chapterNumber, string language)
        {
            await EnsureAuthenticatedAsync();
            try
            {
                // Ensure number formatting matches API expectation (e.g. "10", "10.5")
                var chapterString = chapterNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

                // GET /chapter?manga={id}&chapter={number}&translatedLanguage[]={language}&limit=1&includes[]=scanlation_group
                var url = $"chapter?manga={externalMangaId}&chapter={chapterString}&translatedLanguage[]={language}&limit=1&includes[]=scanlation_group";

                var responseData = await _httpClient.GetAsync(url);

                if (responseData.IsSuccessStatusCode)
                {
                    var response = await responseData.Content.ReadFromJsonAsync<MangaDexResponse<List<ChapterData>>>();

                    if (response?.Data == null || response.Data.Count == 0) return null;

                    var c = response.Data.First();
                    return new ChapterMetadata
                    {
                        Source = ExternalMetadataSource.MangaDex,
                        ExternalMangaId = externalMangaId,
                        ExternalChapterId = c.Id,
                        Language = language,
                        ChapterNumber = c.Attributes.Chapter ?? chapterString,
                        Volume = c.Attributes.Volume,
                        Title = c.Attributes.Title,
                        PublishDate = c.Attributes.PublishAt
                    };
                }
                else
                {
                    // Log failure?
                    Console.WriteLine($"MangaDex Login Failed: {responseData.StatusCode} {await responseData.Content.ReadAsStringAsync()}");
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
