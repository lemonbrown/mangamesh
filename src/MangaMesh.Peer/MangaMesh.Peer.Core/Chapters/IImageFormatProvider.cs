namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>Knows which image file extensions are supported and maps them to MIME types.</summary>
    public interface IImageFormatProvider
    {
        bool IsSupported(string filename);
        string GetMimeType(string filename);
    }
}
