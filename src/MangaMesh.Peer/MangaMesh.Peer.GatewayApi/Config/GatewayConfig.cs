namespace MangaMesh.Peer.GatewayApi.Config;

public class GatewayConfig
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 8080;
    public string TrackerUrl { get; set; } = "https://localhost:7030";
    public int CacheTtlMinutes { get; set; } = 30;
    public string? BootstrapNodes { get; set; }
}
