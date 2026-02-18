using MangaMesh.Peer.ClientApi.Middleware;
using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Data;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Metadata;
using MangaMesh.Peer.Core.Node;
//using MangaMesh.Peer.Core.Replication;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Peer.Core.Subscriptions;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Transport; // For TcpTransport

using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to accept larger request bodies (500MB for manga uploads)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

var trackerUrl = builder.Configuration["TrackerUrl"] ?? "https://localhost:7030";

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

builder.Services.AddControllers();

// Configure form options for large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



builder.Services.Configure<BlobStoreOptions>(builder.Configuration.GetSection("BlobStore"));
builder.Services.Configure<DhtOptions>(builder.Configuration.GetSection("Dht"));

builder.Services.AddScoped<IManifestStore, SqliteManifestStore>();
builder.Services.AddScoped<IStorageMonitorService, StorageMonitorService>();
builder.Services.AddScoped<IBlobStore, BlobStore>();
builder.Services.AddSingleton<ISubscriptionStore>(new SubscriptionStore(
    builder.Configuration["SubscriptionStore:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "input")));
builder.Services.AddSingleton<INodeIdentityService, NodeIdentityService>();


builder.Services.AddDbContext<ClientDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "data", "mangamesh.db")}"));

builder.Services
        .AddScoped<ImportChapterService>()
        .AddScoped<IPeerFetcher, PeerFetcher>()
        .AddSingleton<IKeyPairService, KeyPairService>() // Singleton, depends on Singleton IKeyStore
        .AddScoped<SqliteKeyStore>() // Concrete Scoped
        .AddSingleton<IKeyStore, MangaMesh.Peer.ClientApi.Services.SingletonKeyStore>() // Singleton Wrapper
        .AddScoped<MangaMesh.Peer.ClientApi.Services.IImportChapterService, ImportChapterServiceWrapper>()
        .AddSingleton<INodeConnectionInfoProvider, ServerNodeConnectionInfoProvider>()
        .AddSingleton<IChallengeService, ChallengeService>()
        .AddScoped<IChunkIngester, ChunkIngester>()
        .AddScoped<ITrackerPublisher, TrackerPublisher>()
        .AddSingleton<IImageFormatProvider, DefaultImageFormatProvider>()
        .AddSingleton<IChapterSourceReader, DirectorySourceReader>()
        .AddSingleton<IChapterSourceReader, ZipSourceReader>()
        .AddSingleton<IManifestSigningService, ManifestSigningService>();

builder.Services.AddSingleton<INodeIdentity, NodeIdentity>();



// DHT Node Services
builder.Services.AddSingleton<ITransport>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var port = config.GetValue<int>("Dht:Port", 3001);
    return new TcpTransport(listenPort: port);
});
builder.Services.AddSingleton<IDhtStorage, InMemoryDhtStorage>();
builder.Services.AddSingleton<IBootstrapNodeProvider, YamlBootstrapNodeProvider>();
builder.Services.AddSingleton<IDhtNode>(sp =>
{
    var identity = sp.GetRequiredService<INodeIdentity>();
    var transport = sp.GetRequiredService<ITransport>();
    var storage = sp.GetRequiredService<IDhtStorage>();
    var keyStore = sp.GetRequiredService<IKeyStore>();
    var keypairService = sp.GetRequiredService<IKeyPairService>();
    var tracker = sp.GetRequiredService<INodeAnnouncer>();
    var connectionInfo = sp.GetRequiredService<INodeConnectionInfoProvider>();
    var bootstrapProvider = sp.GetRequiredService<IBootstrapNodeProvider>();
    var logger = sp.GetRequiredService<ILogger<DhtNode>>();
    var routingTable = new KBucketRoutingTable(identity.NodeId);
    var requestTracker = new DhtRequestTracker();
    return new DhtNode(identity, transport, storage, routingTable, bootstrapProvider, requestTracker, keypairService, keyStore, tracker, connectionInfo, logger);
});
builder.Services.AddSingleton<DhtProtocolHandler>();
builder.Services.AddSingleton<ContentProtocolHandler>();
builder.Services.AddSingleton<ProtocolRouter>(sp =>
{
    var router = new ProtocolRouter();
    router.Register(sp.GetRequiredService<DhtProtocolHandler>());
    router.Register(sp.GetRequiredService<ContentProtocolHandler>());
    return router;
});
builder.Services.AddHostedService<DhtHostedService>();

builder.Services.AddMemoryCache();

//builder.Services.AddHostedService<ReplicationService>();

// Logging
var loggerProvider = new MangaMesh.Peer.ClientApi.Services.InMemoryLoggerProvider();
builder.Services.AddSingleton(loggerProvider);
builder.Logging.AddProvider(loggerProvider);

builder.Services.AddHttpClient<IMetadataClient, HttpMetadataClient>(client =>
{
    client.BaseAddress = new Uri("https://metadata.mangamesh.net");
});

builder.Services.AddHttpClient<ITrackerClient, TrackerClient>(client =>
{
    client.BaseAddress = new Uri(trackerUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // Allow self-signed certs in development (e.g. Docker to Host)
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Forward narrower interfaces to the same TrackerClient instance
builder.Services.AddTransient<IPeerLocator>(sp => (IPeerLocator)sp.GetRequiredService<ITrackerClient>());
builder.Services.AddTransient<INodeAnnouncer>(sp => (INodeAnnouncer)sp.GetRequiredService<ITrackerClient>());
builder.Services.AddTransient<ISeriesRegistry>(sp => (ISeriesRegistry)sp.GetRequiredService<ITrackerClient>());
builder.Services.AddTransient<IManifestAnnouncer>(sp => (IManifestAnnouncer)sp.GetRequiredService<ITrackerClient>());
builder.Services.AddTransient<ITrackerChallengeClient>(sp => (ITrackerChallengeClient)sp.GetRequiredService<ITrackerClient>());

builder.Services.AddHttpClient("TrackerProxy", client =>
{
    client.BaseAddress = new Uri(trackerUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
    db.Database.EnsureCreated();

    // Schema Patch: Ensure Manifests table exists (EnsureCreated doesn't migrate existing DBs)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Manifests"" (
            ""Hash"" TEXT NOT NULL CONSTRAINT ""PK_Manifests"" PRIMARY KEY,
            ""SeriesId"" TEXT NOT NULL,
            ""ChapterId"" TEXT NOT NULL,
            ""DataJson"" TEXT NOT NULL,
            ""CreatedUtc"" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_Manifests_SeriesId"" ON ""Manifests"" (""SeriesId"");
        CREATE INDEX IF NOT EXISTS ""IX_Manifests_ChapterId"" ON ""Manifests"" (""ChapterId"");
    ");

    // Migration: specific logic to move keys from JSON to SQLite
    if (!db.Keys.Any())
    {
        var jsonKeyPath = Path.Combine(AppContext.BaseDirectory, "data", "keys", "keys.json");
        if (File.Exists(jsonKeyPath))
        {
            try
            {
                var json = File.ReadAllText(jsonKeyPath);
                var keyPair = System.Text.Json.JsonSerializer.Deserialize<PublicPrivateKeyPair>(json);
                if (keyPair != null)
                {
                    db.Keys.Add(new KeyEntity
                    {
                        PublicKey = keyPair.PublicKeyBase64,
                        PrivateKey = keyPair.PrivateKeyBase64,
                        CreatedAt = DateTime.UtcNow
                    });
                    db.SaveChanges();
                    Console.WriteLine("Migrated keys from JSON to SQLite.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to migrate keys: {ex.Message}");
            }
        }
    }

    // Migration: Manifests
    if (!db.Manifests.Any())
    {
        var manifestDir = Path.Combine(AppContext.BaseDirectory, "input", "manifests");
        if (Directory.Exists(manifestDir))
        {
            var files = Directory.GetFiles(manifestDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var hash = Path.GetFileNameWithoutExtension(file);
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<MangaMesh.Shared.Models.ChapterManifest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (manifest != null)
                    {
                        db.Manifests.Add(new ManifestEntity
                        {
                            Hash = hash,
                            SeriesId = manifest.SeriesId,
                            ChapterId = manifest.ChapterId,
                            DataJson = json, // Keep original JSON strictly
                            CreatedUtc = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to migrate manifest {file}: {ex.Message}");
                }
            }
            if (files.Length > 0)
            {
                db.SaveChanges();
                Console.WriteLine($"Migrated {files.Length} manifests to SQLite.");
            }
        }
    }
}

// Seed DHT storage from persisted manifests so the node announces them
using (var seedScope = app.Services.CreateScope())
{
    var manifestStore = seedScope.ServiceProvider.GetRequiredService<IManifestStore>();
    var dhtStorage = app.Services.GetRequiredService<IDhtStorage>();
    var nodeIdentity = app.Services.GetRequiredService<INodeIdentity>();
    var hashes = await manifestStore.GetAllHashesAsync();
    foreach (var hash in hashes)
    {
        var hashBytes = Convert.FromHexString(hash.Value);
        dhtStorage.StoreContent(hashBytes, nodeIdentity.NodeId);
    }
    if (hashes.Any())
    {
        Console.WriteLine($"Seeded DHT storage with {hashes.Count()} manifest(s) from database.");
    }
}

// Wire transport → router → DHT/Content handlers (mirrors Core peer setup)
var dhtNode = app.Services.GetRequiredService<IDhtNode>();
var transport = app.Services.GetRequiredService<ITransport>();
var router = app.Services.GetRequiredService<ProtocolRouter>();
var dhtHandler = app.Services.GetRequiredService<DhtProtocolHandler>();
var contentHandler = app.Services.GetRequiredService<ContentProtocolHandler>();
dhtHandler.DhtNode = dhtNode;
contentHandler.DhtNode = dhtNode;
if (transport is TcpTransport tcpTransport)
    tcpTransport.OnMessage += router.RouteAsync;

app.UseMiddleware<TrackerProxyMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
