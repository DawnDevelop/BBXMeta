using BeybladeMeta.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Ingestion;

/// <summary>Abstraction over how thread pages are fetched (HttpClient, Playwright, saved files).</summary>
public interface IThreadPageSource
{
    Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default);
}

public sealed record CatchUpOptions(int BackfillMonths = 6, TimeSpan? PageDelay = null)
{
    public TimeSpan Delay => PageDelay ?? TimeSpan.FromSeconds(2);
}

/// <summary>
/// One catch-up pass over the thread. Empty database: backfills from the newest
/// page backwards until posts are older than the configured window. Otherwise:
/// resumes from the last ingested page forward. Ingestion is idempotent per
/// forum post, so re-reading boundary pages is safe.
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

        var resumePage = await db.Posts.MaxAsync(p => (int?)p.Page, ct);
        if (resumePage is null)
        {
            await BackfillAsync(lastPage, probeHtml, ct);
            return;
        }

        log?.Invoke($"Catch-up: resuming from page {resumePage} to {lastPage}.");
        for (var page = resumePage.Value; page <= lastPage; page++)
        {
            var html = page == lastPage ? probeHtml : await source.GetPageHtmlAsync(page, ct);
            var report = await ingestion.IngestHtmlAsync(html, page, ct);
            log?.Invoke($"Page {page}: {report.PostsIngested} new posts, {report.Combos} combos.");
            if (page < lastPage)
                await Task.Delay(options.Delay, ct);
        }
    }

    private async Task BackfillAsync(int lastPage, string lastPageHtml, CancellationToken ct)
    {
        var cutoff = DateTime.Today.AddMonths(-options.BackfillMonths);
        log?.Invoke($"Empty database: backfilling from page {lastPage} back to {cutoff:yyyy-MM-dd}.");

        for (var page = lastPage; page >= 1; page--)
        {
            var html = page == lastPage ? lastPageHtml : await source.GetPageHtmlAsync(page, ct);
            var posts = MyBbPostExtractor.Extract(html);
            await ingestion.IngestHtmlAsync(html, page, ct);

            var oldestDated = posts.Where(p => p.PostedAt is not null).Min(p => p.PostedAt);
            if (oldestDated is not null && oldestDated < cutoff)
            {
                log?.Invoke($"Backfill reached {oldestDated:yyyy-MM-dd} on page {page} — done.");
                return;
            }
            if (page > 1)
                await Task.Delay(options.Delay, ct);
        }
    }
}
