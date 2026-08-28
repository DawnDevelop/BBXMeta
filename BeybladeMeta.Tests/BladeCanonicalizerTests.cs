using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Tests;

public class BladeCanonicalizerTests
{
    [Theory]
    [InlineData("WyvernHover", "Hover Wyvern")]
    [InlineData("WyvernHover", "HoverWyvern")]
    [InlineData("SharkScale", "ScaleShark")]
    [InlineData("SharkScale", "- SharkScale")]
    [InlineData("GolemRock", "Rock Golem")]
    public void Word_order_and_spacing_variants_share_a_key(string a, string b)
    {
        Assert.Equal(BladeCanonicalizer.Key(a), BladeCanonicalizer.Key(b));
    }

    [Fact]
    public void Different_blades_keep_distinct_keys()
    {
        Assert.NotEqual(BladeCanonicalizer.Key("Wizard Rod"), BladeCanonicalizer.Key("Wizard Arrow"));
    }

    [Fact]
    public void Case_and_boundary_variants_merge_via_letter_key()
    {
        // "Sharkscale" (one word) can't token-split, but its letters match "SharkScale".
        var map = BladeCanonicalizer.BuildMap(
            ["SharkScale", "SharkScale", "SharkScale", "Sharkscale", "Shark Scale", "ScaleShark"]);

        Assert.Equal("SharkScale", map["Sharkscale"]);
        Assert.Equal("SharkScale", map["Shark Scale"]);
        Assert.Equal("SharkScale", map["ScaleShark"]); // word-order still merges too
    }

    [Fact]
    public void Typos_fold_into_the_established_blade()
    {
        var blades = new List<string>();
        blades.AddRange(Enumerable.Repeat("CobaltDragoon", 50));
        blades.Add("CobaltDraggon");  // insertion
        blades.Add("CobaltDrgoon");   // deletion
        blades.Add("Colbalt Dragoon"); // transposition (+ space)

        var map = BladeCanonicalizer.BuildMap(blades);

        Assert.Equal("CobaltDragoon", map["CobaltDraggon"]);
        Assert.Equal("CobaltDragoon", map["CobaltDrgoon"]);
        Assert.Equal("CobaltDragoon", map["Colbalt Dragoon"]);
    }

    [Fact]
    public void Part_code_difference_is_not_treated_as_a_typo()
    {
        // "EmperorBlast W" vs "EmperorBlast H" differ by a standalone part code, not a misspelling.
        var blades = new List<string>();
        blades.AddRange(Enumerable.Repeat("EmperorBlast H", 30));
        blades.Add("EmperorBlast W");

        var map = BladeCanonicalizer.BuildMap(blades);

        Assert.NotEqual(map["EmperorBlast H"], map["EmperorBlast W"]);
    }

    [Fact]
    public void Distinct_established_blades_are_never_fuzzy_merged()
    {
        var blades = new List<string>();
        blades.AddRange(Enumerable.Repeat("ImpactDrake", 40));
        blades.AddRange(Enumerable.Repeat("ShelterDrake", 40));

        var map = BladeCanonicalizer.BuildMap(blades);

        Assert.NotEqual(map["ImpactDrake"], map["ShelterDrake"]);
    }

    [Fact]
    public void Build_map_picks_the_most_frequent_spelling_as_canonical()
    {
        // 3× WyvernHover, 2× "Hover Wyvern", 1× HoverWyvern → all map to WyvernHover
        var blades = new[]
        {
            "WyvernHover", "WyvernHover", "WyvernHover",
            "Hover Wyvern", "Hover Wyvern",
            "HoverWyvern",
        };

        var map = BladeCanonicalizer.BuildMap(blades);

        Assert.Equal("WyvernHover", map["WyvernHover"]);
        Assert.Equal("WyvernHover", map["Hover Wyvern"]);
        Assert.Equal("WyvernHover", map["HoverWyvern"]);
    }
}
