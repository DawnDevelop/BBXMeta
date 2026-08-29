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
    // Takara Tomy (JP) ↔ Hasbro (Western) name pairs for the same blade, where the
    // descriptor word (not just word order) differs. Pure word-order swaps are already
    // merged by the token-sort key and need no entry. Keyed by token-sort Key(Hasbro name).
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["wand wizard"] = "WizardRod",      // Wand Wizard = Wizard Rod
        ["sterling wolf"] = "SilverWolf",   // Sterling Wolf = Silver Wolf
        ["phoenix soar"] = "PhoenixWing",   // Soar Phoenix = Phoenix Wing
        ["hammer incendio"] = "HellsHammer", // Hammer Incendio = Hells Hammer
        ["helm knight"] = "KnightShield",   // Helm Knight = Knight Shield
        ["keel shark"] = "SharkEdge",       // Keel Shark = Shark Edge
        ["ptera talon"] = "PteraSwing",     // Talon Ptera = Ptera Swing
        ["garuda scarlet"] = "CrimsonGaruda", // Scarlet Garuda = Crimson Garuda
        ["obsidian shell"] = "BlackShell",  // Obsidian Shell = Black Shell
        ["aether ring"] = "HeavensRing",    // Ring Aether = Heavens Ring
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

        // Fuzzy pass: fold a rare spelling into an established blade one typo away.
        FuzzyMerge(dsu, spellings, counts);

        // Canonical per group = most frequent spelling (ties broken deterministically).
        var canonicalByRoot = spellings
            .GroupBy(dsu.Find)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => counts[s]).ThenBy(s => s, StringComparer.Ordinal).First());

        return spellings.ToDictionary(s => s, s => canonicalByRoot[dsu.Find(s)]);
    }

    // Thresholds for the fuzzy typo pass.
    private const int RareMax = 3;        // a spelling this rare may be a typo
    private const int EstablishedMin = 20; // …of a blade at least this common
    private const int MinLetters = 7;      // skip short names (coincidental 1-edit neighbours)

    /// <summary>
    /// Merge a rare spelling into an established blade when their letters are one edit
    /// apart (insertion/deletion/substitution/transposition). Guards avoid the known
    /// false positives (e.g. "EmperorBlast W" vs "…H", where the difference is a
    /// standalone part code, not a misspelling).
    /// </summary>
    private static void FuzzyMerge(Dsu dsu, List<string> spellings, Dictionary<string, int> counts)
    {
        int GroupCount(string s) => spellings.Where(x => dsu.Find(x) == dsu.Find(s)).Sum(x => counts[x]);
        var established = spellings.Where(s => GroupCount(s) >= EstablishedMin).ToList();

        foreach (var rare in spellings.Where(s => counts[s] <= RareMax && !HasShortToken(s)))
        {
            var rk = LetterKey(rare);
            if (rk.Length < MinLetters)
                continue;
            foreach (var est in established)
            {
                if (dsu.Find(est) == dsu.Find(rare) || HasShortToken(est))
                    continue;
                if (Math.Abs(LetterKey(est).Length - rk.Length) <= 1 && OneEditApart(rk, LetterKey(est)))
                {
                    dsu.Union(rare, est);
                    break;
                }
            }
        }
    }

    // A standalone 1–2 letter token is a part code (W, H, S…), not part of a blade name.
    private static bool HasShortToken(string blade) =>
        blade.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(t => t.Length <= 2);

    /// <summary>True if a and b are within one edit (Damerau/OSA distance ≤ 1).</summary>
    private static bool OneEditApart(string a, string b)
    {
        if (a == b) return true;
        int la = a.Length, lb = b.Length;
        if (Math.Abs(la - lb) > 1) return false;

        if (la == lb)
        {
            int diff = 0, first = -1;
            for (int i = 0; i < la; i++)
                if (a[i] != b[i]) { if (++diff > 2) return false; if (first < 0) first = i; }
            if (diff <= 1) return true;
            // exactly two diffs → allow only an adjacent transposition
            return diff == 2 && first + 1 < la && a[first] == b[first + 1] && a[first + 1] == b[first];
        }

        // lengths differ by one → check for a single insertion/deletion
        var (shorter, longer) = la < lb ? (a, b) : (b, a);
        int si = 0, li = 0; bool skipped = false;
        while (si < shorter.Length && li < longer.Length)
        {
            if (shorter[si] == longer[li]) { si++; li++; }
            else { if (skipped) return false; skipped = true; li++; }
        }
        return true;
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
