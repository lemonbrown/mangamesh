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
        private readonly ILogger<ServerNodeConnectionInfoProvider> _logger;

        public ServerNodeConnectionInfoProvider(IServer server, ILogger<ServerNodeConnectionInfoProvider> logger)
        {
            _server = server;
            _logger = logger;
        }

        public Task<(string IP, int Port)> GetConnectionInfoAsync()
        {
            var ip = GetLocalIpAddress();
            var port = GetPort();

            _logger.LogInformation("Resolved Connection Info: {IP}:{Port}", ip, port);

            return Task.FromResult((ip, port));
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
            try
            {
                var addresses = _server.Features.Get<IServerAddressesFeature>();
                if (addresses != null)
                {
                    foreach (var address in addresses.Addresses)
                    {
                        if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
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
