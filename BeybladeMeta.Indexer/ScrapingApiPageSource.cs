using BeybladeMeta.Core.Ingestion;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Fetches thread pages through a scraping API that renders JS and solves Cloudflare
/// from residential proxies. Service-agnostic via a URL template.
///
/// Environment: SCRAPER_API_KEY (required), SCRAPER_URL_TEMPLATE ({KEY} and {URL}
/// placeholders; {URL} url-encoded, {URL_RAW} verbatim), SCRAPER_MAX_ATTEMPTS (default 3).
/// </summary>
public sealed class ScrapingApiPageSource(HttpClient http, string apiKey, string urlTemplate, int maxAttempts = 3)
    : IThreadPageSource
{
    public static ScrapingApiPageSource FromEnvironment(HttpClient http)
    {
        var key = Environment.GetEnvironmentVariable("SCRAPER_API_KEY")
                  ?? throw new InvalidOperationException("SCRAPER_API_KEY is not set.");
        // wait_for=.post_body holds until posts render, avoiding truncated head-only pages.
        var template = Environment.GetEnvironmentVariable("SCRAPER_URL_TEMPLATE")
                       ?? "https://api.zenrows.com/v1/?apikey={KEY}&url={URL}&js_render=true&premium_proxy=true&wait_for=.post_body";
        var attempts = int.TryParse(Environment.GetEnvironmentVariable("SCRAPER_MAX_ATTEMPTS"), out var a) ? a : 3;
        http.Timeout = TimeSpan.FromSeconds(120); // rendered+solved fetches are slow
        return new ScrapingApiPageSource(http, key, template, attempts);
    }

    public async Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default)
    {
        var target = $"{WboThread.Url}?page={page}";
        var endpoint = urlTemplate
            .Replace("{KEY}", Uri.EscapeDataString(apiKey))
            .Replace("{URL}", Uri.EscapeDataString(target))
            .Replace("{URL_RAW}", target);

        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await http.GetAsync(endpoint, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode && LooksLikeThread(body))
                    return body;
                last = new HttpRequestException(
                    $"Scraper returned {(int)response.StatusCode} without post content for page {page} " +
                    $"(attempt {attempt}/{maxAttempts}).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
        }
        throw last ?? new HttpRequestException($"Failed to fetch page {page}.");
    }

    // Guard against the API returning a Cloudflare interstitial or truncated page as a 200.
    private static bool LooksLikeThread(string html) =>
        html.Contains("post_body", StringComparison.OrdinalIgnoreCase)
        && !html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);
}
