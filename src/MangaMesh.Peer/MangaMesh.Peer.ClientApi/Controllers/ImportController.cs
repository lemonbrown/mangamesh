using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly IImportChapterService _importer;
        private readonly string _inputDirectory = Path.Combine(AppContext.BaseDirectory, "input");
        private readonly string _importedChaptersFile;

        public ImportController(IImportChapterService importer)
        {
            _importer = importer;
            _importedChaptersFile = Path.Combine(_inputDirectory, "imported_chapters.json");
        }


        [HttpGet("chapters")]
        public async Task<ActionResult<IEnumerable<ImportChapterRequestDto>>> GetImportedChapters()
        {
            if (!System.IO.File.Exists(_importedChaptersFile))
            {
                return Ok(new List<ImportChapterRequestDto>());
            }

            var json = await System.IO.File.ReadAllTextAsync(_importedChaptersFile);
            var chapters = JsonSerializer.Deserialize<IEnumerable<ImportChapterRequestDto>>(json);

            return Ok(chapters);
        }

        [HttpPost("chapter")]
        public async Task<ActionResult<ImportResultDto>> ImportChapter(
            [FromBody] ImportChapterRequestDto request)
        {
            Console.WriteLine($"=== Import Chapter Request ===");
            Console.WriteLine($"SeriesId: {request.SeriesId}");
            Console.WriteLine($"ChapterNumber: {request.ChapterNumber}");
            Console.WriteLine($"SourcePath: {request.SourcePath}");
            Console.WriteLine($"Source: {request.Source}");
            Console.WriteLine($"ExternalMangaId: {request.ExternalMangaId}");
            Console.WriteLine($"==============================");

            ImportResultDto result;
            try
            {
                result = await _importer.ImportAsync(request);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Manifest already exists"))
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // General error
                Console.WriteLine($"ERROR: Import failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return BadRequest(ex.Message);
            }

            if (!Directory.Exists(_inputDirectory))
            {
                Directory.CreateDirectory(_inputDirectory);
            }

            var importedChapters = new List<ImportChapterRequestDto>();
            if (System.IO.File.Exists(_importedChaptersFile))
            {
                var json = await System.IO.File.ReadAllTextAsync(_importedChaptersFile);
                importedChapters = JsonSerializer.Deserialize<List<ImportChapterRequestDto>>(json);
            }

            importedChapters.Add(request);
            var newJson = JsonSerializer.Serialize(importedChapters, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(_importedChaptersFile, newJson);

            return Ok(result);
        }

        [HttpPost("reannounce/{hash}")]
        public async Task<ActionResult> ReannounceManifest(string hash, [FromQuery] string nodeId = "mangamesh-node")
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
            {
                return BadRequest("Invalid manifest hash format.");
            }

            try
            {
                await _importer.ReannounceAsync(manifestHash, nodeId);
                return Ok();
            }
            catch (FileNotFoundException)
            {
                return NotFound($"Manifest {hash} not found.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(524_288_000)] // 500 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
        public async Task<ActionResult<List<AnalyzedChapterDto>>> UploadChapter(
            [FromForm] IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            // Create a unique batch ID
            var batchId = Guid.NewGuid().ToString("N");
            var batchPath = Path.Combine(_inputDirectory, "uploads", batchId);
            Directory.CreateDirectory(batchPath);

            var filePaths = new List<string>();

            // Save all files
            foreach (var file in files)
            {
                // We use FileName because webkitRelativePath is often passed here by browsers/clients
                // expecting directory structure preservation.
                // Sanitize the path to avoid directory traversal
                var relativePath = file.FileName.Replace("..", "").TrimStart('/', '\\');
                var fullPath = Path.Combine(batchPath, relativePath);

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Check for archive
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (ext == ".zip" || ext == ".cbz")
                {
                    try
                    {
                        var extractPath = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath));
                        Directory.CreateDirectory(extractPath);
                        System.IO.Compression.ZipFile.ExtractToDirectory(fullPath, extractPath);

                        // Add all extracted files to the list for analysis
                        filePaths.AddRange(Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories));
                    }
                    catch (Exception ex)
                    {
                        // Log or ignore extraction failures, treat as regular file
                        Console.WriteLine($"Failed to extract {fullPath}: {ex.Message}");
                        filePaths.Add(fullPath);
                    }
                }
                else
                {
                    filePaths.Add(fullPath);
                }
            }

            // Analyze directory structure to identify "chapters"
            // We assume leaf directories containing images are chapters
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            // Group files by their parent directory
            var chapters = filePaths
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .GroupBy(Path.GetDirectoryName)
                .Select(g =>
                {
                    var dirName = Path.GetFileName(g.Key!);

                    // Try to parse a number from the directory name
                    // Strategy: look for the last number in the string
                    var number = ExtractChapterNumber(dirName);

                    return new AnalyzedChapterDto(g.Key!, number);
                })
                .ToList();

            return Ok(chapters);
        }

        private static float ExtractChapterNumber(string name)
        {
            // Simple heuristic: find all numbers, take the last one.
            // "Chapter 1" -> 1
            // "Vol 2 Ch 15.5" -> 15.5
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(name, @"\d+(\.\d+)?");
                if (matches.Count > 0)
                {
                    return float.Parse(matches[^1].Value);
                }
            }
            catch { }

            return 0;
        }

    }

    public record AnalyzedChapterDto(string SourcePath, float SuggestedChapterNumber);
}
