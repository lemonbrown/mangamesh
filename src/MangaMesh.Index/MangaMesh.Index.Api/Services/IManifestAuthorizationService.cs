namespace MangaMesh.Index.Api.Services
{
    public interface IManifestAuthorizationService
    {
        void Authorize(string nodeId, string manifestHash);
        bool Consume(string nodeId, string manifestHash);
    }
}
