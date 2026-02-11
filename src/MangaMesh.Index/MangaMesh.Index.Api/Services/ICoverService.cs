namespace MangaMesh.Index.Api.Services
{
    public interface ICoverService
    {
        Task EnsureCoverCachedAsync(string mangaId);
    }
}
