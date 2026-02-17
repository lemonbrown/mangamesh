using MangaMesh.SeriesScraper;
using MangaMesh.SeriesScraper.Core;
using MangaMesh.SeriesScraper.Data;
using MangaMesh.SeriesScraper.Services;
using MangaMesh.SeriesScraper.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddCommandLine(args);
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        var dbPath = config["Scraper:Database:Path"] ?? "./data/scraper.db";
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        services.AddDbContext<ScraperDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddHttpClient<TcbScansOneChapterScraper>();

        services.AddTransient<IMangaChapterScraper, TcbScansOneChapterScraper>();
        services.AddSingleton<IScraperFactory, ScraperFactory>();
        services.AddSingleton<IChapterDownloadService, ChapterDownloadService>();
        services.AddSingleton<IChapterArchiveService, ChapterArchiveService>();
        services.AddSingleton<IMetadataMapper, MetadataMapper>();

        services.AddHostedService<ScraperCliService>();
    });

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScraperDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
