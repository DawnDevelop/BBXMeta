using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;

namespace BeybladeMeta.Web;

public sealed class ThreadIndexOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>How far back the initial backfill reaches.</summary>
    public int BackfillMonths { get; set; } = 6;
    /// <summary>Delay between scheduled catch-up runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    /// <summary>Politeness delay between page fetches.</summary>
    public TimeSpan PageDelay { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>Runs a CatchUpIndexer pass on startup and on a schedule.</summary>
public sealed class ThreadIndexWorker(
    IServiceScopeFactory scopeFactory,
    ThreadIndexOptions options,
    ILogger<ThreadIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var indexer = new CatchUpIndexer(
                    scope.ServiceProvider.GetRequiredService<MetaDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IngestionService>(),
                    scope.ServiceProvider.GetRequiredService<ThreadClient>(),
                    new CatchUpOptions(options.BackfillMonths, options.PageDelay),
                    message => logger.LogInformation("{Message}", message));
                await indexer.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Thread fetch failed ({Message}) — the forum may be blocking server requests. " +
                                  "Retrying in {Interval}; manual HTML upload via /ingest still works.", ex.Message, options.Interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Thread indexing failed; retrying in {Interval}.", options.Interval);
            }

            await Task.Delay(options.Interval, stoppingToken);
        }
    }
}
