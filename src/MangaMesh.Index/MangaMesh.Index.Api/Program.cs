using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using MangaMesh.Index.Api.Services;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddControllers()
    .AddApplicationPart(typeof(MangaMesh.Index.AdminApi.Controllers.DashboardController).Assembly);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<IManifestEntryStore, SqliteManifestEntryStore>();
builder.Services.AddScoped<ISeriesRegistry, SqliteSeriesRegistry>();
builder.Services.AddScoped<ISeriesStore, ManifestDerivedSeriesStore>();
builder.Services.AddSwaggerGen();


//builder.Services.AddHttpClient<IMangaMetadataProvider, MangaDexClient>(client =>
//{
//    client.BaseAddress = new Uri("https://api.mangadex.com");
//});

//builder.Services.AddSingleton<IMangaMetadataProvider>(new JsonMangaMetadataProvider(AppContext.BaseDirectory + "data\\metadata", "metadata.json"));
builder.Services.AddHttpClient<IMangaMetadataProvider, MangaDexMetadataProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.mangadex.org");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MangaMesh/1.0 (mangamesh@example.com)");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false
});

builder.Services.AddSingleton<IMangaMetadataProvider>(sp =>
    new MangaDexMetadataProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MangaDexMetadataProvider)),
        "lemonbrown",
        "QGBq2Wi2JDrHfKR",
        sp.GetRequiredService<ILogger<MangaDexMetadataProvider>>()
    ));

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


// Use explicit path to ensure both APIs use the same database
var dbPath = "/app/data/tracker.db";
var dbDir = Path.GetDirectoryName(dbPath);
if (!Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir!);
    Console.WriteLine($"[DEBUG] Index API - Created directory: {dbDir}");
}
Console.WriteLine($"[DEBUG] Index API - Database Path: {dbPath}");
Console.WriteLine($"[DEBUG] Index API - AppContext.BaseDirectory: {AppContext.BaseDirectory}");
builder.Services.AddDbContext<IndexDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));


builder.Services.AddScoped<ICoverService, CoverService>();

builder.Services
    .AddScoped<IPublicKeyRegistry, PublicKeyRegistry>()
    .AddScoped<IPublicKeyStore, SqlitePublicKeyStore>()
    .AddScoped<IChallengeStore, SqliteChallengeStore>()
    .AddScoped<IApprovedKeyStore, SqliteApprovedKeyStore>()
    .AddSingleton<MangaMesh.Shared.Services.IManifestAuthorizationService, MangaMesh.Shared.Services.ManifestAuthorizationService>()

    //.AddSingleton<ISeriesStore>(new JsonSeriesStore())
    .AddSingleton<INodeRegistry, NodeRegistry>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

var coversPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "covers");
if (!Directory.Exists(coversPath))
{
    Directory.CreateDirectory(coversPath);
}

app.UseStaticFiles(); // Default to wwwroot

app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IndexDbContext>();
    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(dataDir);

    // Schema Patch
    if (db.Database.IsRelational())
    {
        db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Keys"" (
            ""PublicKeyBase64"" TEXT NOT NULL CONSTRAINT ""PK_Keys"" PRIMARY KEY,
            ""RegisteredAt"" TEXT NOT NULL,
            ""Revoked"" INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS ""Challenges"" (
            ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Challenges"" PRIMARY KEY,
            ""UserId"" TEXT NOT NULL,
            ""Nonce"" TEXT NOT NULL,
            ""ExpiresAt"" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS ""ApprovedKeys"" (
            ""PublicKeyBase64"" TEXT NOT NULL CONSTRAINT ""PK_ApprovedKeys"" PRIMARY KEY,
            ""Comment"" TEXT NOT NULL,
            ""AddedAt"" TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS ""ManifestEntries"" (
            ""ManifestHash"" TEXT NOT NULL CONSTRAINT ""PK_ManifestEntries"" PRIMARY KEY,
            ""SeriesId"" TEXT NOT NULL,
            ""ChapterId"" TEXT NOT NULL,
            ""ChapterNumber"" REAL NOT NULL,
            ""Volume"" TEXT,
            ""Language"" TEXT NOT NULL,
            ""ScanGroup"" TEXT,
            ""Quality"" TEXT,
            ""AnnouncedUtc"" TEXT NOT NULL,
            ""LastSeenUtc"" TEXT NOT NULL,
            ""Title"" TEXT NOT NULL,
            ""ExternalMetadataSource"" TEXT NOT NULL,
            ""ExteralMetadataMangaId"" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_ManifestEntries_SeriesId"" ON ""ManifestEntries"" (""SeriesId"");
        CREATE INDEX IF NOT EXISTS ""IX_ManifestEntries_ChapterId"" ON ""ManifestEntries"" (""ChapterId"");
        CREATE TABLE IF NOT EXISTS ""SeriesDefinitions"" (
            ""SeriesId"" TEXT NOT NULL CONSTRAINT ""PK_SeriesDefinitions"" PRIMARY KEY,
            ""Source"" INTEGER NOT NULL,
            ""ExternalMangaId"" TEXT NOT NULL,
            ""Title"" TEXT NOT NULL,
            ""CreatedUtc"" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_SeriesDefinitions_Source_ExternalMangaId"" ON ""SeriesDefinitions"" (""Source"", ""ExternalMangaId"");

    ");
    }

    // Migration: Public Keys
    if (!db.Keys.Any())
    {
        var jsonPath = Path.Combine(dataDir, "keys", "public_keys.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var keys = System.Text.Json.JsonSerializer.Deserialize<List<PublicKeyRecord>>(json);
                if (keys != null)
                {
                    foreach (var k in keys)
                    {
                        // Deduplicate in memory if needed or rely on EF check
                        if (!db.Keys.Any(existing => existing.PublicKeyBase64 == k.PublicKeyBase64))
                        {
                            db.Keys.Add(new IndexKeyEntity
                            {
                                PublicKeyBase64 = k.PublicKeyBase64,
                                RegisteredAt = k.RegisteredAt,
                                Revoked = k.Revoked
                            });
                        }
                    }
                    db.SaveChanges();
                    Console.WriteLine($"Migrated {keys.Count} public keys to SQLite.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to migrate keys: {ex.Message}"); }
        }
    }

    // Migration: Challenges (Optional, they are ephemeral, but why not)
    if (!db.Challenges.Any())
    {
        var jsonPath = Path.Combine(dataDir, "challenges", "challenges.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var challenges = System.Text.Json.JsonSerializer.Deserialize<List<KeyChallenge>>(json);
                if (challenges != null)
                {
                    foreach (var c in challenges)
                    {
                        if (c.ExpiresAt > DateTime.UtcNow) // Only migrate valid ones
                        {
                            db.Challenges.Add(new IndexChallengeEntity
                            {
                                Id = c.Id,
                                UserId = c.UserId,
                                Nonce = c.Nonce,
                                ExpiresAt = c.ExpiresAt
                            });
                        }
                    }
                    db.SaveChanges();
                    Console.WriteLine($"Migrated valid challenges to SQLite.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to migrate challenges: {ex.Message}"); }
        }
    }

    // Migration: Manifest Entries (from JSON files)
    if (!db.ManifestEntries.Any())
    {
        var manifestDir = Path.Combine(dataDir, "manifestentries");
        if (Directory.Exists(manifestDir))
        {
            try
            {
                var files = Directory.GetFiles(manifestDir, "*.json");
                var count = 0;
                foreach (var file in files)
                {
                    var json = File.ReadAllText(file);
                    var entry = System.Text.Json.JsonSerializer.Deserialize<ManifestEntry>(json);
                    if (entry != null && !string.IsNullOrEmpty(entry.ManifestHash))
                    {
                        db.ManifestEntries.Add(new ManifestEntryEntity
                        {
                            ManifestHash = entry.ManifestHash,
                            SeriesId = entry.SeriesId,
                            ChapterId = entry.ChapterId,
                            ChapterNumber = entry.ChapterNumber,
                            Volume = entry.Volume,
                            Language = entry.Language,
                            ScanGroup = entry.ScanGroup,
                            Quality = entry.Quality,
                            AnnouncedUtc = entry.AnnouncedUtc,
                            LastSeenUtc = entry.LastSeenUtc,
                            Title = entry.Title,
                            ExternalMetadataSource = entry.ExternalMetadataSource,
                            ExteralMetadataMangaId = entry.ExteralMetadataMangaId
                        });
                        count++;
                    }
                }
                if (count > 0)
                {
                    db.SaveChanges();
                    Console.WriteLine($"Migrated {count} manifest entries to SQLite.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to migrate manifest entries: {ex.Message}"); }
        }
    }

    // Migration: Series Registry (from JSON file)
    if (!db.SeriesDefinitions.Any())
    {
        var seriesPath = Path.Combine(dataDir, "series", "registry.json");
        if (File.Exists(seriesPath))
        {
            try
            {
                var json = File.ReadAllText(seriesPath);
                var series = System.Text.Json.JsonSerializer.Deserialize<List<SeriesDefinition>>(json);
                if (series != null)
                {
                    foreach (var s in series)
                    {
                        db.SeriesDefinitions.Add(new SeriesDefinitionEntity
                        {
                            SeriesId = s.SeriesId,
                            Source = (int)s.Source,
                            ExternalMangaId = s.ExternalMangaId,
                            Title = s.Title,
                            CreatedUtc = s.CreatedUtc
                        });
                    }
                    db.SaveChanges();
                    Console.WriteLine($"Migrated {series.Count} series definitions to SQLite.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to migrate series registry: {ex.Message}"); }
        }
    }
}

app.MapControllers();

app.Run();

