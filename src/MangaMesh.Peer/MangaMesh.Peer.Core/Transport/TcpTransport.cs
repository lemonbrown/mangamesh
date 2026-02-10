using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class TcpTransport : ITransport
    {
        private readonly int _listenPort;
        private readonly TcpListener _listener;
        
        public event Func<NodeAddress, ReadOnlyMemory<byte>, Task>? OnMessage;

        public TcpTransport(int listenPort)
        {
            _listenPort = listenPort;
            _listener = new TcpListener(IPAddress.Any, _listenPort);
            _listener.Start();

            Task.Run(AcceptLoopAsync);
        }

        public int Port => _listenPort;

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                try 
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch { await Task.Delay(100); }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                var senderIp = remoteEndPoint?.Address.ToString() ?? "unknown";
                var senderPort = remoteEndPoint?.Port ?? 0;

                var lengthBuffer = new byte[4];
                while (client.Connected)
                {
                    int bytesRead = await ReadExactAsync(stream, lengthBuffer, 0, 4);
                    if (bytesRead == 0) break; // Disconnected
                    
                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    
                    if (length < 0 || length > 10 * 1024 * 1024) // 10MB limit check
                    {
                         break; 
                    }
                    
                    var payload = new byte[length];
                    bytesRead = await ReadExactAsync(stream, payload, 0, length);
                    if (bytesRead != length) break;
                    
                    if (OnMessage != null)
                    {
                         await OnMessage.Invoke(new NodeAddress(senderIp, senderPort), new ReadOnlyMemory<byte>(payload));
                    }
                }
            }
            catch { /* connection error */ }
            finally { client.Close(); }
        }
        
        private async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        public async Task SendAsync(NodeAddress to, ReadOnlyMemory<byte> payload)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(to.Host, to.Port);
                using var stream = client.GetStream();
                
                var length = BitConverter.GetBytes(payload.Length);
                await stream.WriteAsync(length, 0, 4);
                await stream.WriteAsync(payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] Send failed: {ex.Message}");
            }
        }
    }
}
