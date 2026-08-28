using BeybladeMeta.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Ingestion;

/// <summary>Abstraction over how thread pages are fetched (HttpClient, Playwright, saved files).</summary>
public interface IThreadPageSource
{
    Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default);
}

public sealed record CatchUpOptions(int MinBackfillPage = 100, TimeSpan? PageDelay = null)
{
    public TimeSpan Delay => PageDelay ?? TimeSpan.FromSeconds(2);
}

/// <summary>
/// One catch-up pass over the thread, always moving forward through pages.
/// First run (empty database): indexes from <see cref="CatchUpOptions.MinBackfillPage"/>
/// (default 100, ~Feb 2) to the current last page. Later runs: resume from the last
/// indexed page — re-reading it because the last page keeps gaining posts. Ingestion
/// is idempotent per forum post, so re-reading a page only adds genuinely new posts.
/// </summary>
public sealed class CatchUpIndexer(
    MetaDbContext db,
    IngestionService ingestion,
    IThreadPageSource source,
    CatchUpOptions options,
    Action<string>? log = null)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        // A huge page number makes MyBB clamp to the thread's real last page.
        var probeHtml = await source.GetPageHtmlAsync(999999, ct);
        var lastPage = MyBbPostExtractor.GetLastPageNumber(probeHtml);

        var lastIndexed = await db.Posts.MaxAsync(p => (int?)p.Page, ct);
        var startPage = lastIndexed is null
            ? Math.Min(options.MinBackfillPage, lastPage)  // first run: floor, clamped if thread is shorter
            : Math.Max(lastIndexed.Value, options.MinBackfillPage); // resume: re-read the last indexed page

        log?.Invoke(lastIndexed is null
            ? $"First run: backfilling pages {startPage}–{lastPage}."
            : $"Catch-up: pages {startPage}–{lastPage} (last indexed was {lastIndexed}).");

        for (var page = startPage; page <= lastPage; page++)
        {
            var html = page == lastPage ? probeHtml : await source.GetPageHtmlAsync(page, ct);
            var report = await ingestion.IngestHtmlAsync(html, page, ct);
            log?.Invoke($"Page {page}: {report.PostsIngested} new posts, {report.Combos} combos.");
            if (page < lastPage)
                await Task.Delay(options.Delay, ct);
        }
    }
}
