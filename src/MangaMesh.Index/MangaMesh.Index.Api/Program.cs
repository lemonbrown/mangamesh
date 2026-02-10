using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddApplicationPart(typeof(MangaMesh.Index.AdminApi.Controllers.DashboardController).Assembly);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IManifestEntryStore, JsonManifestEntryStore>();
builder.Services.AddSingleton<ISeriesRegistry, JsonSeriesRegistry>();
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
        "QGBq2Wi2JDrHfKR"
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


builder.Services.AddDbContext<IndexDbContext>(options =>
    options.UseSqlite($"Data Source={AppContext.BaseDirectory}data\\tracker.db"));

builder.Services
    .AddScoped<IPublicKeyRegistry, PublicKeyRegistry>()
    .AddScoped<IPublicKeyStore, SqlitePublicKeyStore>()
    .AddScoped<IChallengeStore, SqliteChallengeStore>()
    .AddScoped<IApprovedKeyStore, SqliteApprovedKeyStore>()
    .AddSingleton<IManifestAuthorizationService, ManifestAuthorizationService>()

    //.AddSingleton<ISeriesStore>(new JsonSeriesStore())
    .AddSingleton<INodeRegistry, NodeRegistry>();

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
}

app.MapControllers();

app.Run();

