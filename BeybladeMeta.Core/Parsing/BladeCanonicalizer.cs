using System.Text.RegularExpressions;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Merges blade-name spelling variants by linking spellings that share either a
/// token-sort key (letters lowercased and alphabetised — collapses word-order,
/// spacing and CamelCase: "WyvernHover" = "Hover Wyvern") or a letters-only key
/// (collapses case and word-boundary differences: "SharkScale" = "Sharkscale").
/// Both link transitively, so all forms of a blade land in one group. True synonyms
/// that use different words ("Wand Wizard" = "Wizard Rod") are handled by <see cref="Aliases"/>.
/// </summary>
public static partial class BladeCanonicalizer
{
    [GeneratedRegex(@"[A-Za-z][a-z]*")]
    private static partial Regex Words();

    [GeneratedRegex(@"[^A-Za-z]")]
    private static partial Regex NonLetters();

    /// <summary>Explicit synonym map: token-sort Key(variant) → canonical spelling.</summary>
    // Takara Tomy (JP) ↔ Hasbro (Western) name pairs for the same blade.
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["wand wizard"] = "WizardRod",    // Hasbro Wand Wizard = TT Wizard Rod
        ["sterling wolf"] = "SilverWolf", // Hasbro Sterling Wolf = TT Silver Wolf
        ["phoenix soar"] = "PhoenixWing", // Hasbro Soar Phoenix = TT Phoenix Wing
    };

    /// <summary>Word-order/spacing-invariant key: words lowercased and alphabetised.</summary>
    public static string Key(string blade) =>
        string.Join(' ', Words().Matches(blade).Select(m => m.Value.ToLowerInvariant()).OrderBy(w => w, StringComparer.Ordinal));

    /// <summary>Case/boundary-invariant key: letters only, lowercased, order preserved.</summary>
    private static string LetterKey(string blade) => NonLetters().Replace(blade, "").ToLowerInvariant();

    /// <summary>Maps every observed spelling to a canonical one (the most frequent in its group).</summary>
    public static Dictionary<string, string> BuildMap(IEnumerable<string> blades)
    {
        var counts = new Dictionary<string, int>();
        foreach (var b in blades)
            counts[b] = counts.GetValueOrDefault(b) + 1;

        var spellings = counts.Keys.ToList();
        var dsu = new Dsu(spellings);

        // Link spellings that share a token-sort key, and those that share a letter key.
        foreach (var group in spellings.GroupBy(Key))
            LinkAll(dsu, group);
        foreach (var group in spellings.GroupBy(LetterKey))
            LinkAll(dsu, group);
        // Apply explicit synonyms: link the variant to its canonical target.
        foreach (var s in spellings)
            if (Aliases.TryGetValue(Key(s), out var target) && counts.ContainsKey(target))
                dsu.Union(s, target);

        // Canonical per group = most frequent spelling (ties broken deterministically).
        var canonicalByRoot = spellings
            .GroupBy(dsu.Find)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => counts[s]).ThenBy(s => s, StringComparer.Ordinal).First());

        return spellings.ToDictionary(s => s, s => canonicalByRoot[dsu.Find(s)]);
    }

    private static void LinkAll(Dsu dsu, IEnumerable<string> group)
    {
        string? first = null;
        foreach (var s in group)
        {
            if (first is null) first = s;
            else dsu.Union(first, s);
        }
    }

    private sealed class Dsu
    {
        private readonly Dictionary<string, string> _parent;

        public Dsu(IEnumerable<string> items) => _parent = items.ToDictionary(i => i, i => i);

        public string Find(string x)
        {
            while (_parent[x] != x)
                x = _parent[x] = _parent[_parent[x]];
            return x;
        }

        public void Union(string a, string b) => _parent[Find(a)] = Find(b);
    }
}
