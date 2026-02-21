namespace MangaMesh.Peer.GatewayApi.Config;

public enum GatewayMode { Proxy, PeerRedirect }

public class GatewayConfig
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 8080;
    public string TrackerUrl { get; set; } = "https://localhost:7030";
    public int CacheTtlMinutes { get; set; } = 30;
    public string? BootstrapNodes { get; set; }
    public GatewayMode Mode { get; set; } = GatewayMode.Proxy;
    public int PeerClientApiPort { get; set; } = 5202;
    /// <summary>
    /// When set, peer redirect URLs use this host instead of internal container IPs.
    /// Required for Docker/NAT environments where internal IPs aren't reachable from the browser.
    /// Example: "localhost" or "myserver.com"
    /// </summary>
    public string? PeerPublicHost { get; set; }
}
