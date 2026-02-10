using MangaMesh.Client.Content;
using MangaMesh.Client.Helpers;
using MangaMesh.Client.Keys;
using MangaMesh.Client.Node;
using MangaMesh.Client.Transport;
using MangaMesh.GatewayApi.Config;
using MangaMesh.GatewayApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MangaMesh.Client.Manifests;
using MangaMesh.Client.Storage;
using MangaMesh.Client.Blob;

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

// The Gateway Service itself
builder.Services.AddSingleton<GatewayService>();

// Dummy content provider for ContentProtocolHandler (Gateway doesn't really serve content yet, just requests it)
// But to satisfy the constructor:
builder.Services.AddSingleton<Func<string, byte[]?>>(sp => (hash) => null);


builder.Services.AddDbContext<MangaMesh.Client.Data.ClientDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "data", "mangamesh.db")}"));

// Manifest Store
builder.Services.AddSingleton<IManifestStore, MangaMesh.Client.Manifests.SqliteManifestStore>();

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
    var db = scope.ServiceProvider.GetRequiredService<MangaMesh.Client.Data.ClientDbContext>();
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
    // Generating identity if needed is handled inside DhtNode.Start
    dhtNode.StartWithMaintenance(enableBootstrap: true);
    Console.WriteLine($"[Gateway] Started Mesh Node on port {config.Port}");
}

app.Run();

public partial class Program { }
