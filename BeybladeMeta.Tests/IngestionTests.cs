using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;
using BeybladeMeta.Core.Parsing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Tests;

public class IngestionTests : IDisposable
{
    // Mirrors the real worldbeyblade.org MyBB markup: date inside an <a> in the
    // post_date span, format "Jan. 09, 2024"; author lives in a username span.
    private const string PageHtml = """
        <html><body>
        <div class="post classic" id="post_900001">
          <div class="author_information"><span class="username group-id-20"><a href="/User-Reporter">Reporter</a></span></div>
          <div class="post_head"><span class="post_date"><a href="#pid900001" title="Time Posted">Aug. 17, 2026&nbsp;&nbsp;3:12 PM</a></span></div>
          <div class="post_body scaleimages" id="pid_900001">
            Tournament results!<br>
            1st @"Blader001"<br>
            MeteorDragoon 7-60Level (First Stage &amp; Final Stage)<br>
            SharkScale 3-60Rush (First Stage &amp; Final Stage)<br>
            WizardRod 1-60Hexa (First Stage &amp; Final Stage)<br>
            2nd - Blader002<br>
            CobaltDragoon 9-60Elevate<br>
            Valor Bison Glide<br>
          </div>
        </div>
        <div class="post classic" id="post_900002">
          <div class="author_information"><span class="username group-id-20"><a href="/User-Quoter">Quoter</a></span></div>
          <div class="post_body scaleimages" id="pid_900002">
            <blockquote>1st SomeoneElse<br>WizardRod 1-60Hexa</blockquote>
            Congrats everyone, great meta discussion.
          </div>
        </div>
        </body></html>
        """;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    private MetaDbContext CreateContext()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<MetaDbContext>().UseSqlite(_connection).Options;
        var db = new MetaDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Ingests_results_post_and_skips_quoted_results()
    {
        await using var db = CreateContext();
        var service = new IngestionService(db, new PostParser(PartsVocabulary.CreateDefault()));

        var report = await service.IngestHtmlAsync(PageHtml, page: 152);

        Assert.Equal(2, report.PostsSeen);
        Assert.Equal(1, report.PostsIngested); // quote-only post contributes nothing
        Assert.Equal(5, report.Combos);
        Assert.Equal(0, report.UnmatchedLines);

        var ingestedPost = await db.Posts.SingleAsync();
        Assert.Equal(new DateTime(2026, 8, 17), ingestedPost.PostedAt); // date parsed from "Aug. 17, 2026"

        var appearances = await db.Appearances.ToListAsync();
        Assert.Equal(3, appearances.Count(a => a.Placement == 1));
        Assert.Equal(2, appearances.Count(a => a.Placement == 2));
        // The quoted WizardRod result must not have been double-counted
        Assert.Equal(1, appearances.Count(a => a.Display == "WizardRod 1-60Hexa"));
    }

    [Fact]
    public async Task Reingesting_same_page_is_idempotent()
    {
        await using var db = CreateContext();
        var service = new IngestionService(db, new PostParser(PartsVocabulary.CreateDefault()));

        await service.IngestHtmlAsync(PageHtml, page: 152);
        var second = await service.IngestHtmlAsync(PageHtml, page: 152);

        Assert.Equal(0, second.PostsIngested);
        Assert.Equal(5, await db.Appearances.CountAsync());
    }
}
