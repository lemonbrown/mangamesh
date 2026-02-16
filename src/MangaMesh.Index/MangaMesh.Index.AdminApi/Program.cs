using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;
using MangaMesh.Index.AdminApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database - Use explicit path to ensure both APIs use the same database
var dbPath = "/app/data/tracker.db";
var dbDir = Path.GetDirectoryName(dbPath);
if (!Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir!);
    Console.WriteLine($"[DEBUG] Admin API - Created directory: {dbDir}");
}
Console.WriteLine($"[DEBUG] Admin API - Database Path: {dbPath}");
Console.WriteLine($"[DEBUG] Admin API - AppContext.BaseDirectory: {AppContext.BaseDirectory}");
builder.Services.AddDbContext<IndexDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Service Implementations
// Register Configuration
builder.Services.Configure<MangaMesh.Index.AdminApi.Configuration.TrackerSettings>(
    builder.Configuration.GetSection("Tracker"));

// Service Implementations
builder.Services.AddHttpClient<ITrackerService, MangaMesh.Index.AdminApi.Services.HttpTrackerService>();

// builder.Services.AddSingleton<INodeRegistry, NodeRegistry>(); // Removed as we use TrackerService now
builder.Services.AddSingleton<IManifestEntryStore, JsonManifestEntryStore>();
builder.Services.AddSingleton<ISeriesRegistry, JsonSeriesRegistry>();
builder.Services.AddScoped<ISeriesStore, ManifestDerivedSeriesStore>();
builder.Services.AddScoped<IPublicKeyRegistry, PublicKeyRegistry>();
builder.Services.AddScoped<IPublicKeyStore, SqlitePublicKeyStore>();
builder.Services.AddScoped<IApprovedKeyStore, SqliteApprovedKeyStore>();
builder.Services.AddScoped<IChallengeStore, SqliteChallengeStore>();
builder.Services.AddSingleton<IManifestAuthorizationService, ManifestAuthorizationService>();

// Metadata Provider (using fake/cache for now to avoid complexity or just use what Index.Api uses)
// Using JsonMangaMetadataProvider as a fallback or shared one
var metadataDir = Path.Combine(AppContext.BaseDirectory, "data", "metadata");
Directory.CreateDirectory(metadataDir);
builder.Services.AddSingleton<IMangaMetadataProvider>(new JsonMangaMetadataProvider(metadataDir, "metadata.json"));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<IndexDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
