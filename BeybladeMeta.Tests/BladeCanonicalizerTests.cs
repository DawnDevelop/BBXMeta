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
