namespace BeybladeMeta.Core.Ingestion;

/// <summary>
/// Fetches thread pages from worldbeyblade.org with browser-like headers.
/// The site 403s generic clients; if it also blocks this, fall back to
/// manually saved HTML files via IngestionService.IngestHtmlAsync.
/// </summary>
public sealed class ThreadClient(HttpClient http) : IThreadPageSource
{
    public const string ThreadUrl =
        "https://worldbeyblade.org/Thread-Winning-Combinations-at-WBO-Organized-Events-Beyblade-X-BBX";

    /// <summary>
    /// The site sits behind a Cloudflare JS challenge, so plain requests get 403.
    /// Passing the cf_clearance cookie from a real browser session (plus that
    /// browser's exact User-Agent — Cloudflare binds the cookie to it) lets the
    /// indexer through until the cookie expires.
    /// </summary>
    public static void ConfigureClient(HttpClient http, string? userAgent = null, string? cookie = null)
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ??
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        if (!string.IsNullOrWhiteSpace(cookie))
            http.DefaultRequestHeaders.Add("Cookie", cookie);
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"{ThreadUrl}?page={page}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
