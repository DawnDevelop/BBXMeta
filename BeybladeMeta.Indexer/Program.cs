using System.Globalization;
using System.Text.Json;
using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;
using BeybladeMeta.Core.Parsing;
using BeybladeMeta.Indexer;
using Microsoft.EntityFrameworkCore;

var dbPath = Environment.GetEnvironmentVariable("INDEXER_DB") ?? "data/beyblade-meta.db";
var outDir = Environment.GetEnvironmentVariable("INDEXER_OUT") ?? "data";
var backfillMonths = int.TryParse(Environment.GetEnvironmentVariable("INDEXER_BACKFILL_MONTHS"), out var m) ? m : 6;

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
Directory.CreateDirectory(outDir);

var options = new DbContextOptionsBuilder<MetaDbContext>().UseSqlite($"Data Source={dbPath}").Options;
await using var db = new MetaDbContext(options);
db.Database.EnsureCreated();

var ingestion = new IngestionService(db, new PostParser(PartsVocabulary.CreateDefault()));

// SOURCE selects how live pages are fetched: scraper (default) | wayback | playwright.
var sourceKind = (Environment.GetEnvironmentVariable("SOURCE") ?? "scraper").ToLowerInvariant();

try
{
    switch (sourceKind)
    {
        case "wayback":
            using (var http = new HttpClient())
                await new CatchUpIndexer(db, ingestion, new WaybackPageSource(http), new CatchUpOptions(backfillMonths), Console.WriteLine)
                    .RunAsync();
            break;
        case "playwright":
            await using (var source = await PlaywrightPageSource.CreateAsync())
                await new CatchUpIndexer(db, ingestion, source, new CatchUpOptions(backfillMonths), Console.WriteLine)
                    .RunAsync();
            break;
        default: // scraper
            using (var http = new HttpClient())
                await new CatchUpIndexer(db, ingestion, ScrapingApiPageSource.FromEnvironment(http), new CatchUpOptions(backfillMonths), Console.WriteLine)
                    .RunAsync();
            break;
    }
}
catch (Exception ex)
{
    // Export whatever the database already holds so a blocked fetch never wipes the site.
    Console.Error.WriteLine($"Indexing failed: {ex.Message} — exporting existing data only.");
    await ExportAsync(db, outDir);
    return 1;
}

await ExportAsync(db, outDir);
return 0;

static async Task ExportAsync(MetaDbContext db, string outDir)
{
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    var appearances = (await db.Appearances
            .Select(a => new { a.Blade, a.Display, a.Placement, a.Post!.PostedAt })
            .ToListAsync())
        .Select(a => new { a.Blade, a.Display, a.Placement, Week = ToIsoWeek(a.PostedAt) });
    await File.WriteAllTextAsync(Path.Combine(outDir, "appearances.json"),
        JsonSerializer.Serialize(appearances, jsonOptions));

    var unmatched = await db.Unmatched
        .Select(u => new { u.Post!.Page, u.Post!.Author, u.Placement, u.Line })
        .ToListAsync();
    await File.WriteAllTextAsync(Path.Combine(outDir, "unmatched.json"),
        JsonSerializer.Serialize(unmatched, jsonOptions));

    Console.WriteLine($"Exported {appearances.Count()} appearances, {unmatched.Count} unmatched lines to {outDir}.");
}

static string? ToIsoWeek(DateTime? date) =>
    date is null ? null : $"{ISOWeek.GetYear(date.Value)}-W{ISOWeek.GetWeekOfYear(date.Value):00}";
