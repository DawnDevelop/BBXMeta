using BeybladeMeta.Core.Models;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Single source of truth for turning a stored combo into its canonical leaderboard
/// blade and display: canonicalize the bit, merge the blade spelling, then apply CX
/// grouping. Both the exporter and the reprocessor use this so they can never diverge.
/// </summary>
public static class CanonicalCombo
{
    public static (string GroupBlade, string Display) Resolve(
        string blade, string? assistBlade, string? ratchet, string bit,
        IReadOnlyDictionary<string, string> bladeMap, PartsVocabulary vocabulary)
    {
        var spelling = bladeMap.TryGetValue(blade, out var canonical) ? canonical : blade;
        var (groupBlade, displayBladePart) = CxSystem.Resolve(spelling);
        var display = new Combo(displayBladePart, assistBlade, ratchet, vocabulary.CanonicalBit(bit)).Display;
        return (groupBlade, display);
    }
}
