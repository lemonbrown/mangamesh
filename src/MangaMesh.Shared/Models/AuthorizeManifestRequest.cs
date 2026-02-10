namespace MangaMesh.Shared.Models
{
    public class AuthorizeManifestRequest
    {
        public string ChallengeId { get; set; } = "";
        public string SignatureBase64 { get; set; } = "";
        public string ManifestHash { get; set; } = "";
        public string NodeId { get; set; } = "";

        // Optional, but needed if we want to valid the challenge against a specific key 
        // without looking up by "UserId" (which here IS the public key)
        public string PublicKeyBase64 { get; set; } = "";
    }
}
