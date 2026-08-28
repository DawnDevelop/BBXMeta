using HtmlAgilityPack;

namespace BeybladeMeta.Core.Ingestion;

public sealed record ForumPost(string ForumPostId, string Author, string Text, DateTime? PostedAt);

/// <summary>
/// Extracts individual posts from a MyBB thread page (worldbeyblade.org runs MyBB).
/// Quoted posts (blockquotes) are stripped so re-quoted results are not counted twice.
/// </summary>
public static class MyBbPostExtractor
{
    public static IReadOnlyList<ForumPost> Extract(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var bodies = doc.DocumentNode.SelectNodes("//div[contains(@class,'post_body')]");
        if (bodies is null)
            return [];

        var posts = new List<ForumPost>();
        foreach (var body in bodies)
        {
            // MyBB gives the body div an id like "pid_1234567"
            var pid = body.GetAttributeValue("id", "");
            if (!pid.StartsWith("pid_", StringComparison.Ordinal))
                continue;

            foreach (var strip in body.SelectNodes(".//blockquote|.//script|.//style")?.ToList() ?? [])
                strip.Remove();

            var postContainer = FindPostContainer(body);
            var author = postContainer?.SelectSingleNode(".//*[contains(@class,'largetext')]//a")?.InnerText.Trim()
                         ?? postContainer?.SelectSingleNode(".//*[contains(@class,'post_author')]//a")?.InnerText.Trim()
                         ?? "(unknown)";
            var postedAt = ParsePostDate(postContainer?.SelectSingleNode(".//*[contains(@class,'post_date')]"));

            posts.Add(new ForumPost(pid["pid_".Length..], HtmlEntity.DeEntitize(author), ToPlainText(body), postedAt));
        }
        return posts;
    }

    /// <summary>
    /// Reads the highest page number visible in the MyBB pagination bar.
    /// Returns 1 when no pagination is present (single-page thread).
    /// </summary>
    public static int GetLastPageNumber(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var pagination = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'pagination')]");
        if (pagination is null)
            return 1;

        var numbers = System.Text.RegularExpressions.Regex
            .Matches(pagination.InnerHtml, @"page=(\d+)")
            .Select(m => int.Parse(m.Groups[1].Value));
        var current = pagination.SelectSingleNode(".//span[contains(@class,'pagination_current')]");
        if (current is not null && int.TryParse(current.InnerText.Trim(), out var cur))
            numbers = numbers.Append(cur);

        return numbers.DefaultIfEmpty(1).Max();
    }

    /// <summary>
    /// MyBB renders dates like "07-15-2026, 03:12 PM"; recent posts use relative
    /// forms ("Today", "Yesterday", "2 hours ago") which resolve against 'now'.
    /// </summary>
    private static DateTime? ParsePostDate(HtmlNode? dateNode)
    {
        if (dateNode is null)
            return null;
        // First text segment only — MyBB appends edit info in child spans.
        var text = HtmlEntity.DeEntitize(dateNode.ChildNodes
            .Where(n => n.NodeType == HtmlNodeType.Text)
            .Select(n => n.InnerText.Trim())
            .FirstOrDefault(t => t.Length > 0) ?? "").Trim().TrimEnd(',');

        if (text.StartsWith("Today", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ago", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("minute", StringComparison.OrdinalIgnoreCase))
            return DateTime.Today;
        if (text.StartsWith("Yesterday", StringComparison.OrdinalIgnoreCase))
            return DateTime.Today.AddDays(-1);

        var datePart = text.Split(',')[0].Trim();
        return DateTime.TryParseExact(datePart, "MM-dd-yyyy",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out var exact)
            ? exact
            : DateTime.TryParse(datePart, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var loose) ? loose : null;
    }

    private static HtmlNode? FindPostContainer(HtmlNode body)
    {
        for (var node = body.ParentNode; node is not null; node = node.ParentNode)
        {
            if (node.GetAttributeValue("class", "").Split(' ').Contains("post"))
                return node;
        }
        return null;
    }

    private static string ToPlainText(HtmlNode body)
    {
        // <br> and closing block elements become newlines so line-based parsing works.
        var html = body.InnerHtml;
        foreach (var tag in new[] { "<br>", "<br/>", "<br />", "</p>", "</div>", "</li>" })
            html = html.Replace(tag, tag + "\n", StringComparison.OrdinalIgnoreCase);

        var fragment = new HtmlDocument();
        fragment.LoadHtml(html);
        return HtmlEntity.DeEntitize(fragment.DocumentNode.InnerText);
    }
}
