using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Tests;

public class CxSystemTests
{
    [Theory]
    [InlineData("WolfBlast", "Wolf", "Blast")]
    [InlineData("Pegasus Blast", "Pegasus", "Blast")]
    [InlineData("ValkyrieBlast", "Valkyrie", "Blast")]
    [InlineData("PhoenixFlare", "Phoenix", "Flare")]
    [InlineData("DranBrave", "Dran", "Brave")]
    [InlineData("FoxBrush", "Fox", "Brush")]
    public void Cx_blades_split_into_lock_chip_and_main_blade(string blade, string chip, string main)
    {
        var result = CxSystem.TrySplit(blade);
        Assert.NotNull(result);
        Assert.Equal(chip, result.Value.LockChip);
        Assert.Equal(main, result.Value.MainBlade);
    }

    [Theory]
    [InlineData("GloryValkyrie")] // Glory is not a lock chip; Valkyrie is a chip, not a main blade
    [InlineData("SilverWolf")]    // Wolf is a lock chip, not a main blade
    [InlineData("AeroPegasus")]   // Pegasus is a lock chip, not a main blade
    [InlineData("WizardRod")]     // Rod is not a main blade
    [InlineData("CobaltDragoon")] // not CX at all
    [InlineData("ImpactDrake")]   // not CX at all
    [InlineData("SharkScale")]    // basic blade
    public void Fixed_blades_are_not_split(string blade)
    {
        Assert.Null(CxSystem.TrySplit(blade));
    }

    [Fact]
    public void Resolve_groups_cx_by_main_blade_and_keeps_chip_in_display()
    {
        Assert.Equal(("Blast", "Wolf Blast"), CxSystem.Resolve("WolfBlast"));
        Assert.Equal(("Blast", "Emperor Blast"), CxSystem.Resolve("EmperorBlast"));
        Assert.Equal(("SharkScale", "SharkScale"), CxSystem.Resolve("SharkScale"));
    }
}
