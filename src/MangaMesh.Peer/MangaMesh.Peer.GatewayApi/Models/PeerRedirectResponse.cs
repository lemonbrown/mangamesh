namespace MangaMesh.Peer.GatewayApi.Models;

public record PeerRedirectResponse(string Hash, List<string> PeerUrls);
