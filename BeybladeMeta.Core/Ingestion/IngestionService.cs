using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Parsing;
using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Ingestion;

public sealed record IngestReport(int PostsSeen, int PostsIngested, int Combos, int UnmatchedLines);

public sealed class IngestionService(MetaDbContext db, PostParser parser)
{
    /// <summary>Ingest one thread page's HTML (fetched or loaded from a saved file). Already-seen posts are skipped.</summary>
    public async Task<IngestReport> IngestHtmlAsync(string html, int page, CancellationToken ct = default)
    {
        var posts = MyBbPostExtractor.Extract(html);
        int ingested = 0, combos = 0, unmatched = 0;

        foreach (var post in posts)
        {
            if (await db.Posts.AnyAsync(p => p.ForumPostId == post.ForumPostId, ct))
                continue;

            var parsed = parser.Parse(post.Text);
            if (parsed.Placements.Count == 0 && parsed.Unmatched.Count == 0)
                continue; // chatter post, nothing to keep

            var entity = new IngestedPost
            {
                ForumPostId = post.ForumPostId,
                Page = page,
                Author = post.Author,
                RawText = post.Text,
                PostedAt = post.PostedAt,
            };
            db.Posts.Add(entity);

            foreach (var placement in parsed.Placements)
            {
                foreach (var combo in placement.Combos)
                {
                    db.Appearances.Add(new ComboAppearance
                    {
                        Post = entity,
                        Placement = placement.Placement,
                        Player = placement.Player,
                        Blade = combo.Blade,
                        AssistBlade = combo.AssistBlade,
                        Ratchet = combo.Ratchet,
                        Bit = combo.Bit,
                        Display = combo.Display,
                        ComboKey = combo.Key,
                    });
                    combos++;
                }
            }

            foreach (var miss in parsed.Unmatched)
            {
                db.Unmatched.Add(new UnmatchedEntry { Post = entity, Placement = miss.Placement, Line = miss.Line });
                unmatched++;
            }

            ingested++;
        }

        await db.SaveChangesAsync(ct);
        return new IngestReport(posts.Count, ingested, combos, unmatched);
    }
}
