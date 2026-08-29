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
}
