using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Tests;

public class PostParserTests
{
    private readonly PostParser _parser = new(PartsVocabulary.CreateDefault());

    [Fact]
    public void Parses_three_bey_deck_structurally()
    {
        const string post = """
            1st @"Blader001"
            MeteorDragoon 7-60Level (First Stage & Final Stage)
            SharkScale 3-60Rush (First Stage & Final Stage)
            WizardRod 1-60Hexa (First Stage & Final Stage)
            """;

        var result = _parser.Parse(post);

        var placement = Assert.Single(result.Placements);
        Assert.Equal(1, placement.Placement);
        Assert.Equal(
            ["MeteorDragoon 7-60Level", "SharkScale 3-60Rush", "WizardRod 1-60Hexa"],
            placement.Combos.Select(c => c.Display));
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Parses_blade_not_in_any_catalog()
    {
        // WyvernHover/ValkyrieBlast were never in a hardcoded list — structural parsing handles them.
        var result = _parser.Parse("1st X\nWyvernHover 9-60Kick\nValkyrieBlast Heavy9-60Low Rush");

        var combos = result.Placements.Single().Combos;
        Assert.Equal("WyvernHover 9-60Kick", combos[0].Display);
        Assert.Equal("ValkyrieBlast", combos[1].Blade);       // assist split off
        Assert.Equal("Heavy", combos[1].AssistBlade);
        Assert.Equal("Low Rush", combos[1].Bit);
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Cleans_trailing_stage_annotations_from_bit()
    {
        var result = _parser.Parse("1st X\nSharkScale 1-70Low Rush - Finals\nWizardRod 1-60Hexa, First & Final");

        var combos = result.Placements.Single().Combos;
        Assert.Equal("Low Rush", combos[0].Bit);
        Assert.Equal("Hexa", combos[1].Bit);
    }

    [Fact]
    public void Parses_ratchetless_unique_blade_by_trailing_bit()
    {
        var result = _parser.Parse("1st X\nBulletGriffon Hexa\nValor Bison Glide");

        var combos = result.Placements.Single().Combos;
        Assert.Equal("BulletGriffon Hexa", combos[0].Display);
        Assert.Null(combos[0].Ratchet);
        Assert.Equal("Valor Bison Glide", combos[1].Display);
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Captures_all_three_placements_and_ignores_fourth()
    {
        const string post = """
            1st: X
            WizardRod 1-60Hexa
            2nd - Y
            SharkScale 3-60Rush
            3rd Z
            CobaltDragoon 9-60Elevate
            4th W
            DranSword 3-60Flat
            """;

        var result = _parser.Parse(post);

        Assert.Equal([1, 2, 3], result.Placements.Select(p => p.Placement));
        Assert.DoesNotContain(result.Placements, p => p.Combos.Any(c => c.Blade == "DranSword"));
    }

    [Theory]
    [InlineData("Own finish")]
    [InlineData("Out-of-bounds finish")]
    [InlineData("Registered deck list")]
    [InlineData("Check out our Instagram!")]
    [InlineData("Painted blades allowed")]
    public void Non_combo_prose_is_skipped_not_flagged(string line)
    {
        var result = _parser.Parse($"1st X\nWizardRod 1-60Hexa\n{line}");

        Assert.Single(result.Placements.Single().Combos);
        Assert.Empty(result.Unmatched); // prose is ignored, not counted as an unmatched combo
    }

    [Fact]
    public void Spacing_variants_resolve_to_same_combo_key()
    {
        var a = _parser.Parse("1st A\nWizardRod 1-60Hexa").Placements[0].Combos[0];
        var b = _parser.Parse("1st B\nWizardRod 1-60 Hexa").Placements[0].Combos[0];

        Assert.Equal(a.Key, b.Key);
    }

    [Fact]
    public void Post_without_placements_yields_nothing()
    {
        var result = _parser.Parse("Just chatting about the meta, no results here.");

        Assert.Empty(result.Placements);
        Assert.Empty(result.Unmatched);
    }
}
