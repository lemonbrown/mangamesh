using MangaMesh.SeriesScraper.Core;
using MangaMesh.SeriesScraper.Data;
using MangaMesh.SeriesScraper.Data.Entities;
using MangaMesh.SeriesScraper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUlid;
using Spectre.Console;

namespace MangaMesh.SeriesScraper;

public class ScraperCliService : IHostedService
{
    private readonly IScraperFactory _scraperFactory;
    private readonly IChapterDownloadService _downloadService;
    private readonly IChapterArchiveService _archiveService;
    private readonly IConfiguration _config;
    private readonly ScraperDbContext _dbContext;
    private readonly ILogger<ScraperCliService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ScraperCliService(
        IScraperFactory scraperFactory,
        IChapterDownloadService downloadService,
        IChapterArchiveService archiveService,
        IConfiguration config,
        ScraperDbContext dbContext,
        ILogger<ScraperCliService> logger,
        IHostApplicationLifetime lifetime)
    {
        _scraperFactory = scraperFactory;
        _downloadService = downloadService;
        _archiveService = archiveService;
        _config = config;
        _dbContext = dbContext;
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Write(new FigletText("MangaMesh Scraper").Color(Color.Blue));

            var args = Environment.GetCommandLineArgs();
            var command = ParseCommand(args);

            if (command == null)
            {
                ShowHelp();
                return;
            }

            switch (command.Action)
            {
                case "download":
                    await HandleDownloadAsync(command, cancellationToken);
                    break;
                case "list-chapters":
                    await HandleListChaptersAsync(command, cancellationToken);
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred");
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleDownloadAsync(Command command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.Site) || string.IsNullOrEmpty(command.Series))
        {
            AnsiConsole.MarkupLine("[red]Error: --site and --series are required for download[/]");
            return;
        }

        var scraper = _scraperFactory.GetScraperByName(command.Site);
        var outputDir = _config["Scraper:OutputDirectory"] ?? "./output";
        var enableZipping = _config.GetValue<bool>("Scraper:EnableZipping", true);
        var cleanupAfterZip = _config.GetValue<bool>("Scraper:CleanupAfterZip", false);

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        AnsiConsole.MarkupLine($"[green]Fetching chapter list for [bold]{command.Series}[/]...[/]");
        var allChapters = await scraper.GetAvailableChaptersAsync(command.Series);

        if (allChapters.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No chapters found.[/]");
            return;
        }

        var targets = SelectTargetChapters(command, allChapters);
        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No matching chapters found for the specified selection.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Downloading [bold]{targets.Count}[/] chapter(s)...[/]");

        var runEntity = new ScraperRunEntity
        {
            RunId = Ulid.NewUlid().ToString(),
            SiteName = scraper.SiteName,
            SeriesFilter = command.Series,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.ScraperRuns.Add(runEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .StartAsync(async ctx =>
            {
                var overallTask = ctx.AddTask($"[green]Overall[/]", maxValue: targets.Count);

                foreach (var chapter in targets)
                {
                    var label = chapter.SubChapterNumber.HasValue
                        ? $"Ch. {chapter.ChapterNumber}.{chapter.SubChapterNumber}"
                        : $"Ch. {chapter.ChapterNumber}";

                    var chapterTask = ctx.AddTask($"[blue]{label}[/]", maxValue: 100);

                    var chapterEntity = new ScrapedChapterEntity
                    {
                        SiteName = scraper.SiteName,
                        SeriesSlug = command.Series,
                        ChapterIdentifier = chapter.Identifier,
                        ChapterNumber = chapter.ChapterNumber,
                        SubChapterNumber = chapter.SubChapterNumber,
                        Title = chapter.Title,
                        OutputPath = "",
                        ZipPath = "",
                        Status = ScraperStatus.InProgress,
                        ScrapedAt = DateTime.UtcNow
                    };
                    _dbContext.ScrapedChapters.Add(chapterEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    try
                    {
                        var result = await _downloadService.DownloadChapterAsync(
                            scraper, command.Series, chapter, outputDir, cancellationToken);

                        chapterTask.Increment(70);
                        chapterEntity.OutputPath = result.ChapterPath;
                        chapterEntity.ImageCount = result.ImageCount;

                        if (enableZipping)
                        {
                            var zipPath = await _archiveService.ZipChapterAsync(result.ChapterPath, outputDir);
                            chapterEntity.ZipPath = zipPath;

                            if (cleanupAfterZip)
                                await _archiveService.CleanupSourceDirectory(result.ChapterPath, keepOriginal: false);
                        }

                        chapterEntity.Status = ScraperStatus.Completed;
                        runEntity.ChaptersSucceeded++;
                        chapterTask.Increment(30);

                        AnsiConsole.MarkupLine($"  [green]✓[/] {label} — {result.ImageCount} images" +
                            (enableZipping ? $" → [grey]{Markup.Escape(Path.GetFileName(chapterEntity.ZipPath))}[/]" : ""));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to download {Label}", label);
                        chapterEntity.Status = ScraperStatus.Failed;
                        chapterEntity.ErrorMessage = ex.Message;
                        runEntity.ChaptersFailed++;
                        chapterTask.Value = chapterTask.MaxValue;
                        AnsiConsole.MarkupLine($"  [red]✗[/] {label} — {Markup.Escape(ex.Message)}");
                    }
                    finally
                    {
                        runEntity.ChaptersProcessed++;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        overallTask.Increment(1);
                    }

                    if (!cancellationToken.IsCancellationRequested && chapter != targets[^1])
                        await Task.Delay(TimeSpan.FromMilliseconds(
                            _config.GetValue<int>("Scraper:RequestDelayMs", 1000)), cancellationToken);
                }

                runEntity.CompletedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            });

        AnsiConsole.MarkupLine($"\n[green]Done.[/] {runEntity.ChaptersSucceeded} succeeded, {runEntity.ChaptersFailed} failed.");
    }

    private static IReadOnlyList<ChapterInfo> SelectTargetChapters(Command command, IReadOnlyList<ChapterInfo> all)
    {
        // --all
        if (command.DownloadAll)
            return all.OrderBy(c => c.ChapterNumber).ThenBy(c => c.SubChapterNumber ?? 0).ToList();

        // --from / --to range
        if (command.From.HasValue || command.To.HasValue)
        {
            var from = command.From ?? double.MinValue;
            var to = command.To ?? double.MaxValue;
            return all
                .Where(c => c.ChapterNumber >= from && c.ChapterNumber <= to)
                .OrderBy(c => c.ChapterNumber)
                .ThenBy(c => c.SubChapterNumber ?? 0)
                .ToList();
        }

        // --chapters 1173,1174,1175
        if (command.ChapterNumbers.Count > 0)
        {
            var set = command.ChapterNumbers.ToHashSet();
            return all
                .Where(c => set.Contains(c.ChapterNumber))
                .OrderBy(c => c.ChapterNumber)
                .ThenBy(c => c.SubChapterNumber ?? 0)
                .ToList();
        }

        // --chapter 1175 (single)
        if (command.ChapterNumber.HasValue)
        {
            var match = all.Where(c => c.ChapterNumber == command.ChapterNumber.Value).ToList();
            return match;
        }

        return Array.Empty<ChapterInfo>();
    }

    private async Task HandleListChaptersAsync(Command command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.Site) || string.IsNullOrEmpty(command.Series))
        {
            AnsiConsole.MarkupLine("[red]Error: --site and --series are required for list-chapters[/]");
            return;
        }

        var scraper = _scraperFactory.GetScraperByName(command.Site);

        AnsiConsole.MarkupLine($"[green]Fetching chapters for [bold]{command.Series}[/]...[/]");
        var chapters = await scraper.GetAvailableChaptersAsync(command.Series);

        var table = new Table()
            .AddColumn("Chapter")
            .AddColumn("Date")
            .AddColumn("URL");

        foreach (var chapter in chapters.OrderByDescending(c => c.ChapterNumber))
        {
            var chapterNum = chapter.SubChapterNumber.HasValue
                ? $"{chapter.ChapterNumber}.{chapter.SubChapterNumber}"
                : chapter.ChapterNumber.ToString();

            table.AddRow(
                chapterNum,
                chapter.PublishDate?.ToString("yyyy-MM-dd") ?? "-",
                Markup.Escape(chapter.Url));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{chapters.Count} chapter(s) total[/]");
    }

    private static Command? ParseCommand(string[] args)
    {
        if (args.Length < 2) return null;

        var command = new Command { Action = args[1].ToLowerInvariant() };

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--site" when i + 1 < args.Length:
                    command.Site = args[++i];
                    break;
                case "--series" when i + 1 < args.Length:
                    command.Series = args[++i];
                    break;
                case "--all":
                    command.DownloadAll = true;
                    break;
                case "--chapter" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], out var single))
                        command.ChapterNumber = single;
                    break;
                case "--chapters" when i + 1 < args.Length:
                    foreach (var part in args[++i].Split(','))
                        if (double.TryParse(part.Trim(), out var n))
                            command.ChapterNumbers.Add(n);
                    break;
                case "--from" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], out var from))
                        command.From = from;
                    break;
                case "--to" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], out var to))
                        command.To = to;
                    break;
            }
        }

        return command;
    }

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[yellow]Commands:[/]");
        AnsiConsole.MarkupLine("  [bold]download[/]      Download chapter(s)");
        AnsiConsole.MarkupLine("  [bold]list-chapters[/] List available chapters");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[yellow]Download options:[/]");
        AnsiConsole.MarkupLine("  --site     <name>       Scraper site name (required)");
        AnsiConsole.MarkupLine("  --series   <slug>       Series slug (required)");
        AnsiConsole.MarkupLine("  --all                   Download all available chapters");
        AnsiConsole.MarkupLine("  --chapter  <num>        Download a single chapter");
        AnsiConsole.MarkupLine("  --chapters <n,n,n>      Download specific chapters (comma-separated)");
        AnsiConsole.MarkupLine("  --from     <num>        Download chapters from this number");
        AnsiConsole.MarkupLine("  --to       <num>        Download chapters up to this number");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[yellow]Examples:[/]");
        AnsiConsole.MarkupLine("  download --site TCBScansOnePiece --series one-piece --all");
        AnsiConsole.MarkupLine("  download --site TCBScansOnePiece --series one-piece --chapter 1175");
        AnsiConsole.MarkupLine("  download --site TCBScansOnePiece --series one-piece --chapters 1173,1174,1175");
        AnsiConsole.MarkupLine("  download --site TCBScansOnePiece --series one-piece --from 1170 --to 1175");
        AnsiConsole.MarkupLine("  list-chapters --site TCBScansOnePiece --series one-piece");
    }

    private class Command
    {
        public string Action { get; set; } = "";
        public string? Site { get; set; }
        public string? Series { get; set; }
        public bool DownloadAll { get; set; }
        public double? ChapterNumber { get; set; }
        public List<double> ChapterNumbers { get; set; } = [];
        public double? From { get; set; }
        public double? To { get; set; }
    }
}
