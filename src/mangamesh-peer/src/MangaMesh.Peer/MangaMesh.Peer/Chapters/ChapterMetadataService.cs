using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Client.Chapters
{

    public sealed class ChapterMetadataService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Loads a chapter.json from a folder and maps it to the ChapterMetadata model
        /// </summary>
        /// <param name="folderPath">The folder containing chapter.json</param>
        /// <returns>ChapterMetadata object</returns>
        public async Task<ChapterMetadata> LoadMetadataAsync(string folderPath)
        {
            var metadataPath = Path.Combine(folderPath, "chapter.json");
            if (!File.Exists(metadataPath))
                throw new FileNotFoundException("chapter.json not found", metadataPath);

            await using var fs = File.OpenRead(metadataPath);
            var metadata = await JsonSerializer.DeserializeAsync<ChapterMetadata>(fs, _jsonOptions);

            if (metadata is null)
                throw new InvalidOperationException("Failed to deserialize chapter metadata.");

            // If PageFiles not provided, auto-scan folder
            //if (metadata.PageFiles.Count == 0)
            //{
            //    var imageFiles = Directory.GetFiles(folderPath, "*.*")
            //        .Where(f => f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            //                 || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            //                 || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            //        .Select(Path.GetFileName)
            //        .OrderBy(f => f)
            //        .ToList();

            //    metadata = metadata with { PageFiles = imageFiles };
            //}

            return metadata;
        }
    }
}
