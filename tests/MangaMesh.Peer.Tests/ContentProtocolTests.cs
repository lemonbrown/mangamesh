using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using System.IO;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Tests.Helpers;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Peer.Tests
{
    [TestClass]
    public class ContentProtocolTests
    {
        private List<DhtNode> _nodes = new List<DhtNode>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var node in _nodes)
            {
                node.StopWithMaintenance();
            }
            _nodes.Clear();
        }

        [TestMethod]
        public async Task TestGetManifest()
        {
            // 1. Setup Node A (Requester) and Node B (Responder)
            int portA = 4000;
            int portB = 4001;

            var (nodeA, transportA) = CreateNode(portA);
            var (nodeB, transportB) = CreateNode(portB);

            // 2. Register ContentHandler on Node B
            var routerB = new ProtocolRouter();
            
            // Mock BlobStore
            var mockBlobStore = new Mock<IBlobStore>();
            var testHash = "test-hash-123";
            var testContent = new byte[] { 0xBE, 0xEF };

            mockBlobStore.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == testHash))).Returns(true);
            mockBlobStore.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == testHash)))
                .ReturnsAsync(() => new MemoryStream(testContent));

            var scopeFactoryB = CreateScopeFactory(mockBlobStore.Object);
            var contentHandlerB = new ContentProtocolHandler(transportB, scopeFactoryB);
            
            routerB.Register(contentHandlerB);
            
            transportB.OnMessage += routerB.RouteAsync;
            
            // 3. Node A sends GetManifest to Node B
            var manifestMsg = new GetManifest 
            { 
                ContentHash = testHash,
                SenderPort = portA 
            };
            var json = JsonSerializer.Serialize<ContentMessage>(manifestMsg);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            // Sending raw bytes? ProtocolHandler expects ProtocolKind byte + payload
            var payload = new byte[1 + jsonBytes.Length];
            payload[0] = (byte)ProtocolKind.Content;
            Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

            var addressB = new NodeAddress("127.0.0.1", portB);
            
            var receivedTcs = new TaskCompletionSource<string>();
            var verifyingHandler = new VerifyingContentHandler(receivedTcs); // Expects manifest hash or content? logic below
            
            var routerA = new ProtocolRouter();
            routerA.Register(verifyingHandler);
            transportA.OnMessage += routerA.RouteAsync;

            await transportA.SendAsync(addressB, new ReadOnlyMemory<byte>(payload));

            // 4. Verify reception (Node B receives request, sends response. Node A receives response)
            
            var timeout = Task.Delay(2000);
            var completed = await Task.WhenAny(receivedTcs.Task, timeout);

            if (completed == timeout)
            {
                Assert.Fail("Timed out waiting for ManifestData");
            }

            Assert.AreEqual(testHash, await receivedTcs.Task);
        }

        [TestMethod]
        public async Task TestDhtLookupAndManifestRetrieval()
        {
            int portA = 5000;
            int portB = 5001;

            var (nodeA, transportA) = CreateNode(portA);
            var (nodeB, transportB) = CreateNode(portB);

            // Wiring for Content Protocol
            var routerA = new ProtocolRouter();
            var dhtHandlerA = new DhtProtocolHandler { DhtNode = nodeA };
            
            var manifestContent = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
            var manifestHash = "mm:manifest:123";
            
            var mockBlobStoreA = new Mock<IBlobStore>();
            mockBlobStoreA.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == manifestHash))).Returns(true);
            mockBlobStoreA.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == manifestHash)))
                .ReturnsAsync(() => new MemoryStream(manifestContent));

            var scopeFactoryA = CreateScopeFactory(mockBlobStoreA.Object);
            var contentHandlerA = new ContentProtocolHandler(transportA, scopeFactoryA);
            contentHandlerA.DhtNode = nodeA; // IMPORTANT: DhtNode is needed? 
            // ContentProtocolHandler doesn't use DhtNode for GetManifest handling (except responding).
            // DhtNode property is public.
            // Wait, ContentProtocolHandler ONLY uses DhtNode if it needs to trigger DHT operations or uses identity?
            // "GetManifest" handling logic:
            // var content = await _blobStore.OpenReadAsync...
            // SendContentRequestAsync(m.SenderPort, ...). 
            // It relies on _transport.SendAsync. 
            // Does not strictly need DhtNode unless we added something?
            // In GatewayIntegrationTests logic: contentHandler.DhtNode = _peerNode;
            
            routerA.Register(dhtHandlerA);
            routerA.Register(contentHandlerA);
            transportA.OnMessage += routerA.RouteAsync;

            var routerB = new ProtocolRouter();
            var dhtHandlerB = new DhtProtocolHandler { DhtNode = nodeB };
            
            var receivedTcs = new TaskCompletionSource<byte[]>();
            var verifyingHandlerB = new VerifyingContentHandler(null, receivedTcs); // Only cares about data
            
            routerB.Register(dhtHandlerB);
            routerB.Register(verifyingHandlerB);
            transportB.OnMessage += routerB.RouteAsync;

            nodeA.StartWithMaintenance(enableBootstrap: false);
            nodeB.StartWithMaintenance(enableBootstrap: false);

            // 1. Bootstrap B -> A
            var bootstrapEntry = new RoutingEntry
            {
                Address = new NodeAddress("127.0.0.1", portA),
                NodeId = nodeA.Identity.NodeId, // We happen to know reliability in test
                LastSeenUtc = DateTime.UtcNow
            };
            await nodeB.BootstrapAsync(new[] { bootstrapEntry });

            // 2. Node A announces it has the manifest
            // StoreAsync only takes the hash in the current implementation (it implicitly stores self as provider)
            var manifestHashBytes = System.Text.Encoding.UTF8.GetBytes(manifestHash);
            await nodeA.StoreAsync(manifestHashBytes); 

            // Wait for propagation
            await Task.Delay(500);

            // 3. Node B looks up who has the manifest
            // FindValueAsync returns list of NodeIDs (byte[]) of providers
            var providers = await nodeB.FindValueAsync(manifestHashBytes);
            
            Assert.IsNotNull(providers, "DHT lookup failed");
            Assert.IsTrue(providers.Count > 0, "No providers found");

            // 4. Node B requests content from Node A
            // We know Node A is the peer (address 127.0.0.1:portA).
            var request = new GetManifest 
            { 
                ContentHash = manifestHash,
                SenderPort = portB
            };
            var json = JsonSerializer.Serialize<ContentMessage>(request);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var payload = new byte[1 + jsonBytes.Length];
            payload[0] = (byte)ProtocolKind.Content;
            Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

            await transportB.SendAsync(new NodeAddress("127.0.0.1", portA), new ReadOnlyMemory<byte>(payload));

            // 5. Verify Node B receives ManifestData
            var timeout = Task.Delay(2000);
            var completed = await Task.WhenAny(receivedTcs.Task, timeout);

            if (completed == timeout)
            {
                Assert.Fail("Timed out waiting for Manifest Retrieval");
            }

            var receivedData = await receivedTcs.Task;
            CollectionAssert.AreEqual(manifestContent, receivedData);
        }

        private static IServiceScopeFactory CreateScopeFactory(IBlobStore blobStore, IManifestStore? manifestStore = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(blobStore);
            services.AddSingleton(manifestStore ?? new Mock<IManifestStore>().Object);
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IServiceScopeFactory>();
        }

        private (DhtNode, TcpTransport) CreateNode(int port)
        {
            var (node, transport) = TestNodeFactory.CreateNode(port);
            _nodes.Add(node);
            return (node, transport);
        }

        private class VerifyingContentHandler : IProtocolHandler
        {
            private readonly TaskCompletionSource<string>? _manifestTcs;
            private readonly TaskCompletionSource<byte[]>? _dataTcs;

            public VerifyingContentHandler(TaskCompletionSource<string>? manifestTcs = null, TaskCompletionSource<byte[]>? dataTcs = null)
            {
                _manifestTcs = manifestTcs;
                _dataTcs = dataTcs;
            }

            public ProtocolKind Kind => ProtocolKind.Content;

            public Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload)
            {
                var msg = ContentMessage.Deserialize(payload);
                if (msg is GetManifest m)
                {
                    _manifestTcs?.TrySetResult(m.ContentHash);
                }
                else if (msg is ManifestData d)
                {
                    _dataTcs?.TrySetResult(d.Data);
                    _manifestTcs?.TrySetResult(d.ContentHash); // Also signal this for the first test if needed
                }
                return Task.CompletedTask;
            }
        }

    }
}
