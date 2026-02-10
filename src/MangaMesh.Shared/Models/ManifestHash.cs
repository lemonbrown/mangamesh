using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public readonly record struct ManifestHash(string Value)
    {
        public static ManifestHash Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Invalid manifest hash");

            return new ManifestHash(value);
        }

        public static bool TryParse(string value, out ManifestHash result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default;
                return false;
            }

            result = new ManifestHash(value);
            return true;
        }

        /// <summary>
        /// Computes a deterministic hash for a ChapterManifest.
        /// </summary>
        public static ManifestHash FromManifest(ChapterManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            using var sha256 = SHA256.Create();

            // Step 1: canonicalize manifest data
            var sb = new StringBuilder();
            sb.Append(manifest.SeriesId);
            sb.Append('|');
            sb.Append(manifest.ScanGroup);
            sb.Append('|');
            sb.Append(manifest.Language);
            sb.Append('|');
            sb.Append(manifest.ChapterNumber);
            sb.Append('|');

            // Step 2: sort file paths to guarantee deterministic hash
            foreach (var filePath in manifest.Files.OrderBy(f => f.Path))
            {
                sb.Append(filePath);
                sb.Append('|');
            }

            // Step 3: convert to bytes
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            // Step 4: compute SHA256
            var hashBytes = sha256.ComputeHash(bytes);

            // Step 5: convert to hex string
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return new ManifestHash(hashString);
        }
    }
}
