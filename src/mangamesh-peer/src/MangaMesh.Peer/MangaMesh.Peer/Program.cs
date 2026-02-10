// See https://aka.ms/new-console-template for more information
using MangaMesh.Client.Blob;
using MangaMesh.Client.Data;
using MangaMesh.Client.Helpers;
using MangaMesh.Client.Keys;
using MangaMesh.Client.Manifests;
using MangaMesh.Client.Metadata;
using MangaMesh.Client.Node;
using MangaMesh.Client.Replication;
using MangaMesh.Client.Storage;
using MangaMesh.Client.Tracker;
using MangaMesh.Client.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

Console.WriteLine("                                                   _     \r\n  /\\/\\   __ _ _ __   __ _  __ _    /\\/\\   ___  ___| |__  \r\n /    \\ / _` | '_ \\ / _` |/ _` |  /    \\ / _ \\/ __| '_ \\ \r\n/ /\\/\\ \\ (_| | | | | (_| | (_| | / /\\/\\ \\  __/\\__ \\ | | |\r\n\\/    \\/\\__,_|_| |_|\\__, |\\__,_| \\/    \\/\\___||___/_| |_|\r\n                    |___/                                ");

var root = "input";

var trackerUrl = "https://localhost:7030";

var builder = new HostBuilder().ConfigureServices(services =>
{
    services
    .AddLogging(n => n.AddConsole())
    .AddScoped<ITrackerClient, TrackerClient>()
    .AddScoped<IPeerFetcher, PeerFetcher>()
    .AddSingleton<IManifestStore>(new ManifestStore(root))
    .AddSingleton<IStorageMonitorService>(sp => new StorageMonitorService(root, sp.GetRequiredService<IManifestStore>()))
    .AddSingleton<IBlobStore>(sp => new BlobStore(root, sp.GetRequiredService<IStorageMonitorService>()))
    .AddDbContext<ClientDbContext>(options =>
        options.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "data", "mangamesh.db")}"))
    .AddSingleton<IKeyStore, SqliteKeyStore>()
    .AddSingleton<INodeIdentityService, NodeIdentityService>()
    .AddSingleton<IKeyPairService, KeyPairService>();

    services.AddHttpClient<IMetadataClient, HttpMetadataClient>(client =>
    {
        client.BaseAddress = new Uri("https://metadata.mangamesh.net");
    });

    services.AddHttpClient<ITrackerClient, TrackerClient>(client =>
    {
        client.BaseAddress = new Uri(trackerUrl);
    });


    services.AddSingleton<INodeConnectionInfoProvider, ConsoleNodeConnectionInfoProvider>();

    // ======================================================
    // Node identity (singleton, generates or loads keys)
    // ======================================================
    services.AddSingleton<INodeIdentity, NodeIdentity>();

    // ======================================================
    // Transport (singleton)
    // ======================================================
    services.AddSingleton<ITransport>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var port = config.GetValue<int>("Dht:Port", 3001);
        return new TcpTransport(listenPort: port);
    });

    // ======================================================
    // Storage (singleton)
    // ======================================================
    services.AddSingleton<IDhtStorage, InMemoryDhtStorage>();

    // ======================================================
    // DHT node (singleton)
    // ======================================================
    services.AddSingleton<IDhtNode>(sp =>
    {
        var identity = sp.GetRequiredService<INodeIdentity>();
        var transport = sp.GetRequiredService<ITransport>();
        var storage = sp.GetRequiredService<IDhtStorage>();
        var keyStore = sp.GetRequiredService<IKeyStore>();
        var keypariService = sp.GetRequiredService<IKeyPairService>();
        return new DhtNode(identity, transport, storage, keypariService, keyStore);
    });


    // Hosted service
    services.AddHostedService<DhtHostedService>();

    //services.AddHostedService(provider =>
    //    new ReplicationService(
    //        scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
    //        logger: provider.GetRequiredService<ILogger<ReplicationService>>(),
    //        nodeIdentity: provider.GetRequiredService<INodeIdentityService>(),
    //        connectionInfo: provider.GetRequiredService<INodeConnectionInfoProvider>()
    //    )
    //);
});

Console.WriteLine("Running node...");

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MangaMesh.Client.Data.ClientDbContext>();
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

await host.RunAsync();

Console.ReadLine();