using BeybladeMeta.Core.Models;
using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Tests;

public class PostParserTests
{
    private readonly PostParser _parser = new(PartsVocabulary.CreateDefault());

    [Fact]
    public void Parses_quoted_at_username_with_three_bey_deck()
    {
        const string post = """
            1st @"Joaquin7344"
            MeteorDragoon 7-60Level (First Stage & Final Stage)
            SharkScale 3-60Rush (First Stage & Final Stage)
            WizardRod 1-60Hexa (First Stage & Final Stage)
            """;

        var result = _parser.Parse(post);

        var placement = Assert.Single(result.Placements);
        Assert.Equal(1, placement.Placement);
        Assert.Equal("Joaquin7344", placement.Player);
        Assert.Equal(
            ["MeteorDragoon 7-60Level", "SharkScale 3-60Rush", "WizardRod 1-60Hexa"],
            placement.Combos.Select(c => c.Display));
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Parses_dashed_username_with_deck_header_assist_blade_and_ratchetless_combo()
    {
        const string post = """
            1st - Sikuya

            Final Stage - Registered Deck List:
            CobaltDragoon 9-60Elevate (First Stage & Final Stage)
            Valor Bison Glide (First Stage & Final Stage)
            EmperorWhip OuterWheel5-60Unite (First Stage & Final Stage)
            """;

        var result = _parser.Parse(post);

        var placement = Assert.Single(result.Placements);
        Assert.Equal("Sikuya", placement.Player);
        Assert.Equal(
            ["CobaltDragoon 9-60Elevate", "Valor Bison Glide", "EmperorWhip OuterWheel 5-60Unite"],
            placement.Combos.Select(c => c.Display));
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Parses_bare_username_with_four_bey_deck_and_multiword_bit()
    {
        const string post = """
            1st Rjustice0630
            AeroPegasus 1-50Rush (First Stage & Final Stage)
            CobaltDragoon 5-50Elevate (First Stage & Final Stage)
            SharkScale 9-60Free Ball (First Stage & Final Stage)
            WizardRod 1-60Hexa (First Stage & Final Stage)
            """;

        var result = _parser.Parse(post);

        var placement = Assert.Single(result.Placements);
        Assert.Equal("Rjustice0630", placement.Player);
        Assert.Equal(4, placement.Combos.Count);
        Assert.Equal("SharkScale 9-60FreeBall", placement.Combos[2].Display);
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Captures_all_three_placements_and_ignores_fourth()
    {
        const string post = """
            Results from today's event!

            1st: PlayerOne
            WizardRod 1-60Hexa
            2nd - PlayerTwo
            SharkScale 3-60Rush
            3rd PlayerThree
            CobaltDragoon 9-60Elevate
            4th PlayerFour
            DranSword 3-60Flat
            """;

        var result = _parser.Parse(post);

        Assert.Equal([1, 2, 3], result.Placements.Select(p => p.Placement));
        Assert.Equal(["PlayerOne", "PlayerTwo", "PlayerThree"], result.Placements.Select(p => p.Player));
        Assert.DoesNotContain(result.Placements, p => p.Combos.Any(c => c.Blade == "DranSword"));
    }

    [Fact]
    public void Unknown_part_is_flagged_as_unmatched_not_guessed()
    {
        const string post = """
            1st SomePlayer
            WizardRod 1-60Hexa
            TotallyNewBlade 5-60Hexa
            """;

        var result = _parser.Parse(post);

        var placement = Assert.Single(result.Placements);
        Assert.Single(placement.Combos);
        var miss = Assert.Single(result.Unmatched);
        Assert.Equal(1, miss.Placement);
        Assert.Contains("TotallyNewBlade", miss.Line);
    }

    [Fact]
    public void Spacing_variants_resolve_to_same_combo_key()
    {
        var a = _parser.Parse("1st A\nWizardRod 1-60Hexa").Placements[0].Combos[0];
        var b = _parser.Parse("1st B\nWizard Rod 1-60 Hexa").Placements[0].Combos[0];

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
