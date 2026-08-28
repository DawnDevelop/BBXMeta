using System.Globalization;
using System.Text.Json;
using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;
using BeybladeMeta.Core.Parsing;
using BeybladeMeta.Indexer;
using Microsoft.EntityFrameworkCore;

var dbPath = Environment.GetEnvironmentVariable("INDEXER_DB") ?? "data/beyblade-meta.db";
var outDir = Environment.GetEnvironmentVariable("INDEXER_OUT") ?? "data";
var minPage = int.TryParse(Environment.GetEnvironmentVariable("INDEXER_MIN_PAGE"), out var mp) ? mp : 100;

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
    await new CatchUpIndexer(db, ingestion, source, new CatchUpOptions(minPage), Console.WriteLine).RunAsync();
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
