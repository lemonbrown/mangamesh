namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>Supports the standard manga image formats: JPEG, PNG, WebP.</summary>
    public sealed class DefaultImageFormatProvider : IImageFormatProvider
    {
        private static readonly HashSet<string> _supported = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public bool IsSupported(string filename)
        {
            var ext = Path.GetExtension(filename);
            return !string.IsNullOrEmpty(ext) && _supported.Contains(ext);
        }

        public string GetMimeType(string filename)
        {
            var ext = Path.GetExtension(filename)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".webp"           => "image/webp",
                _                 => "application/octet-stream"
            };
        }
    }
}
