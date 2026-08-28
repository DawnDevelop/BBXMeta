using BeybladeMeta.Core.Ingestion;
using Microsoft.Playwright;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Fetches thread pages through a persistent Chrome profile so a Cloudflare
/// session solved once (manually, on the first headed run) is reused across
/// runs. Stealth init scripts mask the automation signals Cloudflare fingerprints
/// (navigator.webdriver, headless chrome markers, missing plugins).
///
/// Environment:
///   INDEXER_PROFILE_DIR  persistent user-data dir (default ./.chrome-profile)
///   INDEXER_HEADLESS=1   force headless (default headed; CI uses xvfb)
///   INDEXER_CHALLENGE_WAIT_MS  extra ms to allow manual solve on first run
/// </summary>
public sealed class PlaywrightPageSource : IThreadPageSource, IAsyncDisposable
{
    // Trimmed puppeteer-extra-stealth equivalents applied before any page script runs.
    private const string StealthScript = """
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
        Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
        window.chrome = { runtime: {} };
        const origQuery = window.navigator.permissions.query;
        window.navigator.permissions.query = (p) =>
            p.name === 'notifications'
                ? Promise.resolve({ state: Notification.permission })
                : origQuery(p);
        """;

    private readonly IPlaywright _playwright;
    private readonly IBrowserContext _context;
    private readonly IPage _page;
    private readonly int _challengeWaitMs;

    private PlaywrightPageSource(IPlaywright playwright, IBrowserContext context, IPage page, int challengeWaitMs)
    {
        _playwright = playwright;
        _context = context;
        _page = page;
        _challengeWaitMs = challengeWaitMs;
    }

    public static async Task<PlaywrightPageSource> CreateAsync()
    {
        var playwright = await Playwright.CreateAsync();
        var headless = Environment.GetEnvironmentVariable("INDEXER_HEADLESS") == "1";
        var profileDir = Environment.GetEnvironmentVariable("INDEXER_PROFILE_DIR")
                         ?? Path.Combine(Directory.GetCurrentDirectory(), ".chrome-profile");
        var challengeWaitMs = int.TryParse(Environment.GetEnvironmentVariable("INDEXER_CHALLENGE_WAIT_MS"), out var w) ? w : 0;

        var options = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = headless,
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Args = ["--disable-blink-features=AutomationControlled"],
        };

        IBrowserContext context;
        try
        {
            // Real branded Chrome fingerprints cleaner than bundled Chromium.
            context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, Clone(options, "chrome"));
        }
        catch (PlaywrightException)
        {
            context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, Clone(options, null));
        }

        await context.AddInitScriptAsync(StealthScript);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        return new PlaywrightPageSource(playwright, context, page, challengeWaitMs);
    }

    private static BrowserTypeLaunchPersistentContextOptions Clone(BrowserTypeLaunchPersistentContextOptions o, string? channel) => new()
    {
        Headless = o.Headless,
        Locale = o.Locale,
        ViewportSize = o.ViewportSize,
        Args = o.Args,
        Channel = channel,
    };

    /// <summary>
    /// Opens the thread headed and blocks until the operator confirms the forum is
    /// visible (Cloudflare solved). The solved session persists in the profile dir,
    /// so later runs — even headless — reuse it. Run once when indexing starts failing.
    /// </summary>
    public async Task WarmUpAsync()
    {
        await _page.GotoAsync($"{ThreadClient.ThreadUrl}?page=1",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 });
        Console.WriteLine();
        Console.WriteLine("A Chrome window is open on the thread. Complete any Cloudflare check until you");
        Console.WriteLine("can see the forum posts, then press Enter here to save the session…");
        Console.ReadLine();

        var ok = await _page.QuerySelectorAsync("div.post_body, div#posts") is not null;
        Console.WriteLine(ok
            ? "Forum content detected — session saved to the profile."
            : "Warning: forum posts not detected yet. If the page still shows a challenge, re-run --login.");
    }

    public async Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default)
    {
        await _page.GotoAsync($"{ThreadClient.ThreadUrl}?page={page}",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 });

        // If a challenge interstitial is showing, give it time to auto-solve (or be solved by hand on the first headed run).
        if (_challengeWaitMs > 0 && await IsChallengeAsync())
        {
            Console.WriteLine($"Cloudflare challenge detected — waiting up to {_challengeWaitMs}ms (solve it in the window if headed)…");
            await _page.WaitForSelectorAsync("div.post_body, div#posts",
                new PageWaitForSelectorOptions { Timeout = _challengeWaitMs, State = WaitForSelectorState.Attached });
        }
        else
        {
            await _page.WaitForSelectorAsync("div.post_body, div#posts",
                new PageWaitForSelectorOptions { Timeout = 60_000, State = WaitForSelectorState.Attached });
        }

        return await _page.ContentAsync();
    }

    private async Task<bool> IsChallengeAsync()
    {
        var title = await _page.TitleAsync();
        return title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
               || await _page.QuerySelectorAsync("div.post_body, div#posts") is null;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        _playwright.Dispose();
    }
}
