using System.Text.RegularExpressions;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Merges blade-name spelling variants. A token-sort key (letters only, lowercased,
/// alphabetical) collapses word-order, spacing, CamelCase and punctuation variants
/// ("WyvernHover" = "Hover Wyvern" = "- HoverWyvern"). True synonyms that use
/// different words ("Wand Wizard" = "Wizard Rod") can't be inferred and are handled
/// by <see cref="Aliases"/>.
/// </summary>
public static partial class BladeCanonicalizer
{
    [GeneratedRegex(@"[A-Za-z][a-z]*")]
    private static partial Regex Words();

    /// <summary>Explicit synonym map: any spelling (token-sort key) → canonical spelling.</summary>
    // Keyed by token-sort Key(variant) → canonical spelling.
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["wand wizard"] = "WizardRod", // "Wand Wizard" = Wizard Rod (rod/wand synonym)
    };

    public static string Key(string blade) =>
        string.Join(' ', Words().Matches(blade).Select(m => m.Value.ToLowerInvariant()).OrderBy(w => w, StringComparer.Ordinal));

    /// <summary>Maps every observed spelling to a canonical one (the most frequent per key).</summary>
    public static Dictionary<string, string> BuildMap(IEnumerable<string> blades)
    {
        var counts = new Dictionary<string, int>();
        foreach (var b in blades)
            counts[b] = counts.GetValueOrDefault(b) + 1;

        var canonicalByKey = counts
            .GroupBy(kv => Aliases.TryGetValue(Key(kv.Key), out var alias) ? Key(alias) : Key(kv.Key))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key);

        return counts.Keys.ToDictionary(
            spelling => spelling,
            spelling => canonicalByKey[Aliases.TryGetValue(Key(spelling), out var alias) ? Key(alias) : Key(spelling)]);
    }
}
