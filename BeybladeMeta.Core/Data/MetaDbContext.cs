using Microsoft.EntityFrameworkCore;

namespace BeybladeMeta.Core.Data;

public class IngestedPost
{
    public int Id { get; set; }
    /// <summary>MyBB post id ("pid"), unique per forum post — used to skip re-ingestion.</summary>
    public required string ForumPostId { get; set; }
    public int Page { get; set; }
    public required string Author { get; set; }
    public required string RawText { get; set; }
    public DateTime? PostedAt { get; set; }
}

public class ComboAppearance
{
    public int Id { get; set; }
    public int IngestedPostId { get; set; }
    public IngestedPost? Post { get; set; }
    public int Placement { get; set; }
    public required string Player { get; set; }
    public required string Blade { get; set; }
    public string? AssistBlade { get; set; }
    public string? Ratchet { get; set; }
    public required string Bit { get; set; }
    public required string Display { get; set; }
    public required string ComboKey { get; set; }
}

public class UnmatchedEntry
{
    public int Id { get; set; }
    public int IngestedPostId { get; set; }
    public IngestedPost? Post { get; set; }
    public int Placement { get; set; }
    public required string Line { get; set; }
}

public class MetaDbContext(DbContextOptions<MetaDbContext> options) : DbContext(options)
{
    public DbSet<IngestedPost> Posts => Set<IngestedPost>();
    public DbSet<ComboAppearance> Appearances => Set<ComboAppearance>();
    public DbSet<UnmatchedEntry> Unmatched => Set<UnmatchedEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngestedPost>().HasIndex(p => p.ForumPostId).IsUnique();
        modelBuilder.Entity<ComboAppearance>().HasIndex(a => a.ComboKey);
    }
}
