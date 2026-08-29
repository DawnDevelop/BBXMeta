using System.Globalization;
using System.Text.Json;
using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;
using BeybladeMeta.Core.Models;
using BeybladeMeta.Core.Parsing;
using BeybladeMeta.Indexer;
using Microsoft.EntityFrameworkCore;

var dbPath = Environment.GetEnvironmentVariable("INDEXER_DB") ?? "data/beyblade-meta.db";
var outDir = Environment.GetEnvironmentVariable("INDEXER_OUT") ?? "data";
var minPage = int.TryParse(Environment.GetEnvironmentVariable("INDEXER_MIN_PAGE"), out var mp) ? mp : 100;
var concurrency = int.TryParse(Environment.GetEnvironmentVariable("INDEXER_CONCURRENCY"), out var cc) ? cc : 5;

// Offline data fix: re-parse existing exports with the current parser (no scraping).
if (Environment.GetEnvironmentVariable("REPROCESS") == "1")
{
    Reprocessor.Run(outDir);
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
Directory.CreateDirectory(outDir);

var options = new DbContextOptionsBuilder<MetaDbContext>().UseSqlite($"Data Source={dbPath}").Options;
await using var db = new MetaDbContext(options);
db.Database.EnsureCreated();

var ingestion = new IngestionService(db, new PostParser(PartsVocabulary.CreateDefault()));

try
{
    using var http = new HttpClient();
    var source = ScrapingApiPageSource.FromEnvironment(http);
    await new CatchUpIndexer(db, ingestion, source, new CatchUpOptions(minPage, Concurrency: concurrency), Console.WriteLine).RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Indexing failed: {ex.Message}");
    // Only re-export if this run actually ingested something; never overwrite good
    // exports with an empty DB when the very first fetch fails.
    if (await db.Appearances.AnyAsync())
    {
        Console.Error.WriteLine("Exporting the partial data gathered before the failure.");
        await ExportAsync(db, outDir);
    }
    else
    {
        Console.Error.WriteLine("No data ingested — leaving existing exports untouched.");
    }
    return 1;
}

await ExportAsync(db, outDir);
return 0;

static async Task ExportAsync(MetaDbContext db, string outDir)
{
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    var rows = await db.Appearances
        .Select(a => new { a.Blade, a.AssistBlade, a.Ratchet, a.Bit, a.Placement, a.Post!.PostedAt })
        .ToListAsync();

    // Canonicalize bit, blade spelling and CX grouping — same path as the reprocessor.
    var vocab = PartsVocabulary.CreateDefault();
    var bladeMap = BladeCanonicalizer.BuildMap(rows.Select(r => r.Blade));
    var appearances = rows.Select(r =>
    {
        var (blade, display) = CanonicalCombo.Resolve(r.Blade, r.AssistBlade, r.Ratchet, r.Bit, bladeMap, vocab);
        return new { Blade = blade, Display = display, r.Placement, Date = r.PostedAt?.ToString("yyyy-MM-dd") };
    });
    await File.WriteAllTextAsync(Path.Combine(outDir, "appearances.json"),
        JsonSerializer.Serialize(appearances, jsonOptions));

    var unmatched = await db.Unmatched
        .Select(u => new { u.Post!.Page, u.Placement, u.Line })
        .ToListAsync();
    await File.WriteAllTextAsync(Path.Combine(outDir, "unmatched.json"),
        JsonSerializer.Serialize(unmatched, jsonOptions));

    Console.WriteLine($"Exported {appearances.Count()} appearances, {unmatched.Count} unmatched lines to {outDir}.");
}
