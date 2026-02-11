using MangaMesh.Shared.Services;
using Microsoft.Extensions.Hosting; // For IHostEnvironment
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MangaMesh.Index.Api.Services
{
    public class CoverService : ICoverService
    {
        private readonly IMangaMetadataProvider _metadataProvider;
        private readonly ILogger<CoverService> _logger;
        private readonly HttpClient _http;
        private readonly string _coversDir;

        public CoverService(IMangaMetadataProvider metadataProvider, ILogger<CoverService> logger, IHttpClientFactory httpClientFactory, IHostEnvironment env)
        {
            _metadataProvider = metadataProvider;
            _logger = logger;
            _http = httpClientFactory.CreateClient();

            // MangaDex requires a User-Agent for all requests, including image downloads
            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _http.DefaultRequestHeaders.Add("User-Agent", "MangaMesh/1.0 (mangamesh@example.com)");
            }

            // Define storage path - use WebRootPath or match Program.cs logic
            _coversDir = Path.Combine(env.ContentRootPath, "wwwroot", "covers");
            if (!Directory.Exists(_coversDir))
            {
                Directory.CreateDirectory(_coversDir);
            }
        }

        public async Task EnsureCoverCachedAsync(string mangaId)
        {
            try
            {
                var originalPath = Path.Combine(_coversDir, $"{mangaId}.webp");
                var cardPath = Path.Combine(_coversDir, $"{mangaId}.card.webp");
                var thumbPath = Path.Combine(_coversDir, $"{mangaId}.thumb.webp");

                // Check if all exist
                if (File.Exists(originalPath) && File.Exists(cardPath) && File.Exists(thumbPath))
                {
                    // Console.WriteLine($"[CoverService] Covers already exist for {mangaId}");
                    return;
                }

                _logger.LogInformation("Caching covers for series {MangaId}", mangaId);
                Console.WriteLine($"[CoverService] Attempting to cache covers for {mangaId}...");

                var metadata = await _metadataProvider.GetMangaAsync(mangaId);
                if (metadata == null || string.IsNullOrEmpty(metadata.CoverFilename))
                {
                    _logger.LogWarning("No metadata or cover filename found for series {MangaId}", mangaId);
                    return;
                }

                var imageUrl = $"https://uploads.mangadex.org/covers/{mangaId}/{metadata.CoverFilename}";
                Console.WriteLine($"[CoverService] Downloading from: {imageUrl}");

                var imageBytes = await _http.GetByteArrayAsync(imageUrl);

                using (var image = Image.Load(imageBytes))
                {
                    // 1. Save Original
                    if (!File.Exists(originalPath))
                    {
                        await image.SaveAsWebpAsync(originalPath);
                        Console.WriteLine($"[CoverService] Saved original to {originalPath}");
                    }

                    // 2. Save Card (Width 400)
                    if (!File.Exists(cardPath))
                    {
                        using (var cardImage = image.Clone(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(400, 0),
                            Mode = ResizeMode.Max
                        })))
                        {
                            await cardImage.SaveAsWebpAsync(cardPath);
                            Console.WriteLine($"[CoverService] Saved card to {cardPath}");
                        }
                    }

                    // 3. Save Thumb (Width 200)
                    if (!File.Exists(thumbPath))
                    {
                        using (var thumbImage = image.Clone(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(200, 0),
                            Mode = ResizeMode.Max
                        })))
                        {
                            await thumbImage.SaveAsWebpAsync(thumbPath);
                            Console.WriteLine($"[CoverService] Saved thumb to {thumbPath}");
                        }
                    }
                }

                _logger.LogInformation("Successfully cached covers for {MangaId}", mangaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache cover for series {MangaId}", mangaId);
                Console.WriteLine($"[CoverService] ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
