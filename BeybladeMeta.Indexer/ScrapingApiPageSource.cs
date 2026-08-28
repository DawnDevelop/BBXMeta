using BeybladeMeta.Core.Ingestion;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Fetches thread pages through a third-party scraping API that renders JS and
/// solves Cloudflare from residential proxies. Service-agnostic: the endpoint is
/// a URL template so any provider (ScraperAPI, ZenRows, ScrapingBee, …) works by
/// changing configuration only.
///
/// Environment:
///   SCRAPER_API_KEY       the provider API key (required)
///   SCRAPER_URL_TEMPLATE  endpoint template with {KEY} and {URL} placeholders.
///                         {URL} is url-encoded; {URL_RAW} is inserted verbatim.
///   SCRAPER_MAX_ATTEMPTS  per-page retry count (default 3)
///
/// Example templates (fill the real one after picking a provider):
///   ScraperAPI:  https://api.scraperapi.com/?api_key={KEY}&url={URL}&render=true&ultra_premium=true
///   ZenRows:     https://api.zenrows.com/v1/?apikey={KEY}&url={URL}&js_render=true&antibot=true
///   ScrapingBee: https://app.scrapingbee.com/api/v1/?api_key={KEY}&url={URL}&render_js=true&stealth_proxy=true
/// </summary>
public sealed class ScrapingApiPageSource(HttpClient http, string apiKey, string urlTemplate, int maxAttempts = 3)
    : IThreadPageSource
{
    public static ScrapingApiPageSource FromEnvironment(HttpClient http)
    {
        var key = Environment.GetEnvironmentVariable("SCRAPER_API_KEY")
                  ?? throw new InvalidOperationException("SCRAPER_API_KEY is not set.");
        // Defaults to ZenRows (permanent free tier, Cloudflare-solving included).
        // Override SCRAPER_URL_TEMPLATE to switch providers.
        var template = Environment.GetEnvironmentVariable("SCRAPER_URL_TEMPLATE")
                       ?? "https://api.zenrows.com/v1/?apikey={KEY}&url={URL}&js_render=true&premium_proxy=true";
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
                    $"Scraper returned {(int)response.StatusCode} for page {page} (attempt {attempt}/{maxAttempts}).");
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

    // Guard against the API returning a Cloudflare interstitial as a 200.
    private static bool LooksLikeThread(string html) =>
        html.Contains("post_body", StringComparison.OrdinalIgnoreCase)
        && !html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);
}
