namespace MangaMesh.GatewayApi.Config;

    public class GatewayConfig
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 8080;
        public int CacheTtlMinutes { get; set; } = 30;
    }
