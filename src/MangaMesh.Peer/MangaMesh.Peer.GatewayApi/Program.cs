using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Data;
using MangaMesh.Peer.GatewayApi.Config;
using MangaMesh.Peer.GatewayApi.Services;
using MangaMesh.Peer.Core.Tracker; // Add this using for TrackerClient registration
using MangaMesh.Peer.ClientApi.Services; // For ServerNodeConnectionInfoProvider

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Gateway Config
var gatewayConfig = new GatewayConfig();
builder.Configuration.GetSection("Gateway").Bind(gatewayConfig);
builder.Services.AddSingleton(gatewayConfig);

// Gateway Cache Config
builder.Services.Configure<GatewayCacheOptions>(builder.Configuration.GetSection("GatewayCache"));
builder.Services.AddSingleton<IGatewayCache, GatewayCacheService>();

// MangaMesh Services
// Need to register KeyPairService and KeyStore properly
builder.Services.AddSingleton<IKeyPairService, KeyPairService>();
builder.Services.AddSingleton<IKeyStore, SqliteKeyStore>();

// For storage, we need IDhtStorage. InMemoryDhtStorage is internal in Client?
// If so, we might need a workaround or make it public. Assuming it's accessible.
// Actually, InMemoryDhtStorage is in MangaMesh.Client.Node namespace, usually public.
builder.Services.AddSingleton<IDhtStorage, InMemoryDhtStorage>();

builder.Services.AddSingleton<ITransport>(sp => new TcpTransport(sp.GetRequiredService<GatewayConfig>().Port));

// Register Protocol Handlers
builder.Services.AddSingleton<DhtProtocolHandler>();
builder.Services.AddSingleton<ContentProtocolHandler>();

// Register Router
builder.Services.AddSingleton<ProtocolRouter>(sp =>
{
    var router = new ProtocolRouter();
    router.Register(sp.GetRequiredService<DhtProtocolHandler>());
    router.Register(sp.GetRequiredService<ContentProtocolHandler>());
    return router;
});

// Register Identity
builder.Services.AddSingleton<INodeIdentity, NodeIdentity>();

// Register Node
builder.Services.AddSingleton<IDhtNode, DhtNode>();

// Register Tracker Client
builder.Services.AddHttpClient<ITrackerClient, TrackerClient>(client =>
{
    client.BaseAddress = new Uri(gatewayConfig.TrackerUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // Allow self-signed for dev
    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

// Register Connection Info Provider
// For Gateway, we use a provider that returns the configured port or resolves IP
// We can reuse ServerNodeConnectionInfoProvider if we register IServer, unlikely for Gateway (it's a Console App/Worker potentially?)
// Actually GatewayApi IS a WebApplication (builder = WebApplication.CreateBuilder).
// So ServerNodeConnectionInfoProvider is appropriate if it listens on HTTP (it does, MapControllers).
// But DhtNode needs the "Public" IP and DHT Port.
// ServerNodeConnectionInfoProvider gives IP and HTTP Port.
// DhtNode.AnnounceToIndexAsync is hardcoded to use Transport.Port (DHT Port) which is correct.
// But it uses ConnectionInfoProvider for IP.
// So ServerNodeConnectionInfoProvider is fine for IP.
builder.Services.AddSingleton<INodeConnectionInfoProvider, ServerNodeConnectionInfoProvider>();

// The Gateway Service itself
builder.Services.AddSingleton<GatewayService>();

// Dummy content provider for ContentProtocolHandler (Gateway doesn't really serve content yet, just requests it)
// But to satisfy the constructor:
builder.Services.AddSingleton<Func<string, byte[]?>>(sp => (hash) => null);


// Ensure data directory exists
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
if (!Directory.Exists(dataDir))
{
    Directory.CreateDirectory(dataDir);
}

builder.Services.AddDbContext<ClientDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(dataDir, "mangamesh.db")}"));

// Manifest Store
builder.Services.AddSingleton<IManifestStore, SqliteManifestStore>();

// Storage Monitor (monitors blob storage)
var blobRoot = Path.Combine(AppContext.BaseDirectory, "data", "blobs");
builder.Services.AddSingleton<IStorageMonitorService>(sp =>
    new StorageMonitorService(blobRoot, sp.GetRequiredService<IManifestStore>()));

// Blob Store
builder.Services.AddSingleton<IBlobStore>(sp =>
    new BlobStore(blobRoot, sp.GetRequiredService<IStorageMonitorService>()));

// Chunk Ingester
builder.Services.AddSingleton<IChunkIngester, ChunkIngester>();

// Dummy content provider for generic hash lookup if needed, but ContentProtocolHandler now uses IBlobStore
builder.Services.AddSingleton<Func<string, byte[]?>>(sp => (hash) => null);


var app = builder.Build();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

// Start the Mesh Node
var dhtNode = app.Services.GetRequiredService<IDhtNode>();
var transport = app.Services.GetRequiredService<ITransport>();
var router = app.Services.GetRequiredService<ProtocolRouter>();
var config = app.Services.GetRequiredService<GatewayConfig>();

// Wire up transport to router
if (transport is TcpTransport tcp)
{
    tcp.OnMessage += router.RouteAsync;
}

// Wire up DHT to Content Handler (for responses)
var contentHandler = app.Services.GetRequiredService<ContentProtocolHandler>();
contentHandler.DhtNode = dhtNode;

// Start Node
if (config.Enabled)
{
    // Parse bootstrap nodes from config (e.g. "host1:port1,host2:port2")
    List<RoutingEntry>? bootstrapNodes = null;
    if (!string.IsNullOrEmpty(config.BootstrapNodes))
    {
        bootstrapNodes = new List<RoutingEntry>();
        var nodes = config.BootstrapNodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var node in nodes)
        {
            var parts = node.Trim().Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                bootstrapNodes.Add(new RoutingEntry
                {
                    NodeId = Array.Empty<byte>(),
                    Address = new NodeAddress(parts[0], port),
                    LastSeenUtc = DateTime.UtcNow
                });
            }
        }
    }

    dhtNode.StartWithMaintenance(enableBootstrap: true, bootstrapNodes);
    Console.WriteLine($"[Gateway] Started Mesh Node on port {config.Port}");
}

app.Run();

public partial class Program { }
