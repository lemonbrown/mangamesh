using MangaMesh.Shared.Models;
using NSec.Cryptography;

namespace MangaMesh.Shared.Services
{
    public interface IManifestSigningService
    {
        byte[] SerializeCanonical(ChapterManifest manifest);
        SignedChapterManifest SignManifest(ChapterManifest manifest, Key privateKey);
        void VerifySignedManifest(SignedChapterManifest signed);
    }
}
