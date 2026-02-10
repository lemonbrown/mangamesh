using MangaMesh.Client.Blob;
using MangaMesh.Client.Chapters;
using MangaMesh.Client.Keys;
using MangaMesh.Client.Manifests;
using MangaMesh.Client.Metadata;
using MangaMesh.Client.Node;
using MangaMesh.Client.Replication;
using MangaMesh.Client.Storage;
using MangaMesh.Client.Subscriptions;
using MangaMesh.Client.Tracker;
using MangaMesh.Server.Services;
using Microsoft.EntityFrameworkCore;

var dataPath = Path.Combine(AppContext.BaseDirectory, "input");

var builder = WebApplication.CreateBuilder(args);

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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



builder.Services.AddScoped<IManifestStore, SqliteManifestStore>();
builder.Services.AddScoped<IStorageMonitorService>(sp => new StorageMonitorService(dataPath, sp.GetRequiredService<IManifestStore>()));
builder.Services.AddScoped<IBlobStore>(sp => new BlobStore(dataPath, sp.GetRequiredService<IStorageMonitorService>()));
builder.Services.AddSingleton<ISubscriptionStore>(new SubscriptionStore(dataPath));
builder.Services.AddSingleton<INodeIdentityService, NodeIdentityService>();


builder.Services.AddDbContext<MangaMesh.Client.Data.ClientDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "data", "mangamesh.db")}"));

builder.Services
        .AddScoped<ImportChapterService>()
        .AddScoped<IPeerFetcher, PeerFetcher>()
        .AddScoped<IKeyPairService, KeyPairService>()
        .AddScoped<IKeyStore, SqliteKeyStore>()
        .AddScoped<MangaMesh.Server.Services.IImportChapterService, ImportChapterServiceWrapper>()
        .AddSingleton<INodeConnectionInfoProvider, ServerNodeConnectionInfoProvider>()
        .AddSingleton<IChallengeService, ChallengeService>();

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ReplicationService>();

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

app.UseMiddleware<MangaMesh.Server.Middleware.TrackerProxyMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
