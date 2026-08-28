using BeybladeMeta.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Ingestion;

/// <summary>Abstraction over how thread pages are fetched.</summary>
public interface IThreadPageSource
{
    Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default);
}

public sealed record CatchUpOptions(int MinBackfillPage = 100, TimeSpan? PageDelay = null, int Concurrency = 1)
{
    public TimeSpan Delay => PageDelay ?? TimeSpan.FromSeconds(2);
    public int EffectiveConcurrency => Math.Max(1, Concurrency);
}

/// <summary>
/// One forward pass over the thread. First run indexes from
/// <see cref="CatchUpOptions.MinBackfillPage"/> to the last page; later runs resume
/// from the last indexed page, re-reading it since it keeps gaining posts. Ingestion
/// is idempotent per post, so re-reading only adds new posts.
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
        var lastIndexed = await db.Posts.MaxAsync(p => (int?)p.Page, ct);
        var startPage = lastIndexed ?? options.MinBackfillPage;
        if (startPage < options.MinBackfillPage)
            startPage = options.MinBackfillPage;

        // Fetch the start page to learn the last page from its pagination bar.
        // (An out-of-range page clamps to page 1 here, so don't probe with a huge number.)
        var firstHtml = await source.GetPageHtmlAsync(startPage, ct);
        var lastPage = MyBbPostExtractor.GetLastPageNumber(firstHtml);
        if (startPage > lastPage)
        {
            // The floor/last-indexed page overshot a shorter-than-expected thread.
            startPage = lastPage;
            firstHtml = await source.GetPageHtmlAsync(startPage, ct);
        }

        log?.Invoke(lastIndexed is null
            ? $"First run: backfilling pages {startPage}–{lastPage} ({options.EffectiveConcurrency}x parallel)."
            : $"Catch-up: pages {startPage}–{lastPage} (last indexed was {lastIndexed}).");

        var pages = Enumerable.Range(startPage, lastPage - startPage + 1).ToList();

        // Fetch pages concurrently (bounded), but ingest sequentially in page order —
        // SQLite writes and the seen-post check must not run in parallel.
        var gate = new SemaphoreSlim(options.EffectiveConcurrency);
        async Task<string> FetchAsync(int page)
        {
            if (page == startPage)
                return firstHtml; // already fetched to discover the last page
            await gate.WaitAsync(ct);
            try { return await source.GetPageHtmlAsync(page, ct); }
            finally { gate.Release(); }
        }

        var fetches = pages.ToDictionary(p => p, FetchAsync);
        try
        {
            foreach (var page in pages)
            {
                var html = await fetches[page];
                var report = await ingestion.IngestHtmlAsync(html, page, ct);
                log?.Invoke($"Page {page}: {report.PostsIngested} new posts, {report.Combos} combos.");
            }
        }
        finally
        {
            // Observe any remaining fetch exceptions so they don't go unobserved.
            await Task.WhenAll(fetches.Values.Select(t => t.ContinueWith(_ => { }, TaskScheduler.Default)));
        }
    }
}
