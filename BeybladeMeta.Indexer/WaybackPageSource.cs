using System.Text.Json;
using BeybladeMeta.Core.Ingestion;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Reads thread pages from the Wayback Machine — free, no Cloudflare, works from
/// any IP. Coverage is limited to pages archive.org has already captured, so this
/// is used for the historical backfill only; recent pages come from a live source.
/// </summary>
public sealed class WaybackPageSource(HttpClient http) : IThreadPageSource
{
    private const string CdxApi = "http://web.archive.org/cdx/search/cdx";

    /// <summary>Page numbers archive.org has a 200/text-html snapshot for.</summary>
    public async Task<IReadOnlyList<int>> GetArchivedPagesAsync(CancellationToken ct = default)
    {
        var url = $"{CdxApi}?url={Uri.EscapeDataString("worldbeyblade.org/" + ThreadSlug + "?page=*")}" +
                  "&output=json&filter=statuscode:200&filter=mimetype:text/html&collapse=urlkey";
        var json = await http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement;
        var pages = new SortedSet<int>();
        for (var i = 1; i < rows.GetArrayLength(); i++) // row 0 is the header
        {
            var original = rows[i][2].GetString() ?? "";
            var idx = original.IndexOf("page=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && int.TryParse(new string(original[(idx + 5)..].TakeWhile(char.IsDigit).ToArray()), out var p))
                pages.Add(p);
        }
        return pages.ToList();
    }

    public async Task<string> GetPageHtmlAsync(int page, CancellationToken ct = default)
    {
        // "2im_" = latest snapshot, id_ suffix = raw archived bytes without the Wayback toolbar.
        var target = $"https://worldbeyblade.org/{ThreadSlug}?page={page}";
        var url = $"https://web.archive.org/web/2im_/{target}";
        return await http.GetStringAsync(url, ct);
    }

    private const string ThreadSlug = "Thread-Winning-Combinations-at-WBO-Organized-Events-Beyblade-X-BBX";
}
