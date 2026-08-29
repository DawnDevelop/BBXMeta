using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Tests;

public class CanonicalComboTests
{
    private static readonly PartsVocabulary Vocab = PartsVocabulary.CreateDefault();
    private static readonly IReadOnlyDictionary<string, string> Identity = new Dictionary<string, string>();

    [Fact]
    public void Export_path_canonicalizes_the_bit_abbreviation()
    {
        // The DB may hold a raw abbreviated bit ("LR"); the export must still merge it.
        var (blade, display) = CanonicalCombo.Resolve("SharkScale", null, "1-70", "LR", Identity, Vocab);
        Assert.Equal("SharkScale", blade);
        Assert.Equal("SharkScale 1-70LowRush", display);
    }

    [Fact]
    public void Export_path_groups_cx_and_keeps_lock_chip_in_display()
    {
        var (blade, display) = CanonicalCombo.Resolve("WolfBlast", "Heavy", "3-60", "R", Identity, Vocab);
        Assert.Equal("Blast", blade);
        Assert.Equal("Wolf Blast Heavy 3-60Rush", display);
    }

    [Fact]
    public void Stuck_assist_code_is_split_off_so_cx_grouping_works()
    {
        // Old DB rows have the assist code stuck in the blade ("ValkyrieBlast J", no assist field).
        var (blade, assist) = CanonicalCombo.CleanBlade("ValkyrieBlast J", null, Vocab);
        Assert.Equal("ValkyrieBlast", blade);
        Assert.Equal("Jaggy", assist);

        var (group, display) = CanonicalCombo.Resolve(blade, assist, "3-60", "LR", Identity, Vocab);
        Assert.Equal("Blast", group); // now groups under Blast instead of "ValkyrieBlast J"
        Assert.Equal("Valkyrie Blast Jaggy 3-60LowRush", display);
    }
}
