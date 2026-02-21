using MangaMesh.Peer.Core.Node;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Net;
using System.Net.Sockets;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class ServerNodeConnectionInfoProvider : INodeConnectionInfoProvider
    {
        private readonly IServer _server;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServerNodeConnectionInfoProvider> _logger;

        public ServerNodeConnectionInfoProvider(IServer server, IConfiguration configuration, ILogger<ServerNodeConnectionInfoProvider> logger)
        {
            _server = server;
            _configuration = configuration;
            _logger = logger;
        }

        public Task<(string IP, int DhtPort, int HttpApiPort)> GetConnectionInfoAsync()
        {
            var ip = GetLocalIpAddress();
            var httpPort = GetPort();

            _logger.LogInformation("Resolved Connection Info: IP={IP}, HttpApiPort={HttpApiPort}", ip, httpPort);

            return Task.FromResult((ip, 0, httpPort));
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve local IP");
            }
            return "127.0.0.1";
        }

        private int GetPort()
        {
            // Try ASPNETCORE_URLS configuration first (most reliable in Docker)
            var urls = _configuration["ASPNETCORE_URLS"];
            if (!string.IsNullOrEmpty(urls))
            {
                var firstUrl = urls.Split(';')[0]
                    .Replace("://+:", "://localhost:")
                    .Replace("://*:", "://localhost:");
                if (Uri.TryCreate(firstUrl, UriKind.Absolute, out var configUri))
                {
                    return configUri.Port;
                }
            }

            try
            {
                var addresses = _server.Features.Get<IServerAddressesFeature>();
                if (addresses != null)
                {
                    foreach (var address in addresses.Addresses)
                    {
                        // Replace wildcard hosts (+, *) so Uri can parse the port
                        var normalised = address
                            .Replace("://+:", "://localhost:")
                            .Replace("://*:", "://localhost:");
                        if (Uri.TryCreate(normalised, UriKind.Absolute, out var uri))
                        {
                            return uri.Port;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve server port");
            }
            return 5000; // Default
        }
    }
}
