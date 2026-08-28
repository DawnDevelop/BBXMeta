using BeybladeMeta.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Ingestion;

/// <summary>Abstraction over how thread pages are fetched (HttpClient, Playwright, saved files).</summary>
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
            ? $"First run: backfilling pages {startPage}–{lastPage} ({options.EffectiveConcurrency}x parallel)."
            : $"Catch-up: pages {startPage}–{lastPage} (last indexed was {lastIndexed}).");

        if (startPage > lastPage)
            return;

        var pages = Enumerable.Range(startPage, lastPage - startPage + 1).ToList();

        // Fetch pages concurrently (bounded), but ingest sequentially in page order —
        // SQLite writes and the seen-post check must not run in parallel.
        var gate = new SemaphoreSlim(options.EffectiveConcurrency);
        async Task<string> FetchAsync(int page)
        {
            if (page == lastPage)
                return probeHtml; // already fetched by the probe
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
