using System.Text.RegularExpressions;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// The Beyblade X CX (Custom) system: a blade is a Lock Chip + Main Blade + Assist
/// Blade, with the lock chip and assist swappable. A name is CX only when its prefix
/// is a real lock chip AND its suffix is a real main blade — so "ValkyrieBlast" splits
/// (Valkyrie chip + Blast blade) but "GloryValkyrie" and "SilverWolf" do not, because
/// their suffixes (Valkyrie, Wolf) are lock chips, not main blades.
/// Lists sourced from the Beyblade wiki (Custom Line parts).
/// </summary>
public static partial class CxSystem
{
    public static readonly HashSet<string> LockChips = Set(
        "Valkyrie", "Dran", "Wizard", "Perseus", "Hells", "Rhino", "Fox", "Pegasus",
        "Cerberus", "Sol", "Wolf", "Emperor", "Phoenix", "Bahamut", "Knight", "Ragna",
        "Unicorn", "Kraken");

    public static readonly HashSet<string> MainBlades = Set(
        "Volt", "Brave", "Arc", "Dark", "Reaper", "Brush", "Blast", "Flame", "Eclipse",
        "Hunt", "Might", "Flare");

    public static readonly HashSet<string> AssistBlades = Set(
        "Slash", "Round", "Bumper", "Turn", "Charge", "Jaggy", "Assault", "Wheel",
        "Dual", "Free", "Heavy", "Zillion", "Knuckle", "Vertical", "Erase", "OuterWheel");

    [GeneratedRegex(@"[A-Za-z][a-z]*")]
    private static partial Regex Words();

    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// If <paramref name="blade"/> is a CX blade (lock chip + main blade), returns the
    /// canonical (LockChip, MainBlade); otherwise null.
    /// </summary>
    public static (string LockChip, string MainBlade)? TrySplit(string blade)
    {
        var words = Words().Matches(blade).Select(m => m.Value).ToList();
        if (words.Count < 2)
            return null;
        var main = words[^1];
        var chip = string.Concat(words[..^1]);
        return MainBlades.TryGetValue(main, out var mainCanonical) && LockChips.TryGetValue(chip, out var chipCanonical)
            ? (chipCanonical, mainCanonical)
            : null;
    }

    /// <summary>
    /// Resolves a blade to how it should appear on the leaderboard: CX blades group under
    /// their main blade, with the lock chip kept only in the display; others are unchanged.
    /// </summary>
    public static (string GroupBlade, string DisplayBladePart) Resolve(string blade) =>
        TrySplit(blade) is var (chip, main) && chip is not null
            ? (main, $"{chip} {main}")
            : (blade, blade);
}
