namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Supports the parser's non-structural needs: splitting a CX assist part off the
/// blade, recognizing ratchet-less combos by their trailing bit, and canonicalizing
/// bit names so abbreviations merge with full names ("Fb"/"FreeBall" → "Free Ball").
/// </summary>
public sealed class PartsVocabulary
{
    private readonly Dictionary<string, string> _bitCanonical; // normalized form -> canonical full name
    private readonly HashSet<string> _assists;                 // normalized assist-blade names
    private readonly int _maxBitWords;

    public PartsVocabulary(IEnumerable<string> bits, IEnumerable<string> assistBlades)
    {
        var bitList = bits.Distinct().ToList();
        _bitCanonical = BuildBitMap(bitList);
        _assists = assistBlades.Select(Normalize).ToHashSet();
        _maxBitWords = bitList.Count == 0 ? 1 : bitList.Select(b => b.Split(' ').Length).Max();
    }

    public static string Normalize(string s) =>
        s.Replace(" ", "").Replace(" ", "").ToLowerInvariant().Trim();

    /// <summary>Standard bit code: first letter for one word, initials for several.</summary>
    private static string Abbreviate(string bit)
    {
        var words = bit.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 1 ? words[0][..1] : string.Concat(words.Select(w => w[..1]));
    }

    private static Dictionary<string, string> BuildBitMap(List<string> bits)
    {
        var map = new Dictionary<string, string>();
        // Full names (and their spaceless forms) always map to themselves.
        foreach (var bit in bits)
            map[Normalize(bit)] = bit;

        // Abbreviations map to the full name, but only when unambiguous.
        var byAbbr = bits.GroupBy(b => Normalize(Abbreviate(b)));
        foreach (var group in byAbbr)
        {
            if (group.Count() != 1)
                continue; // colliding code (e.g. Wall Ball vs Wide Ball) — leave separate
            var abbr = group.Key;
            if (!map.ContainsKey(abbr)) // don't let an abbreviation shadow a real full name
                map[abbr] = group.Single();
        }
        return map;
    }

    /// <summary>Canonical full-name form of a bit; returns the input trimmed if unknown.</summary>
    public string CanonicalBit(string raw)
    {
        var trimmed = raw.Trim();
        return _bitCanonical.TryGetValue(Normalize(trimmed), out var canonical) ? canonical : trimmed;
    }

    public bool IsAssist(string token) => _assists.Contains(Normalize(token));

    /// <summary>
    /// If the trailing 1..N tokens form a known bit, returns (bladePart, canonicalBit);
    /// used only for ratchet-less lines. Null when no trailing bit is recognized.
    /// </summary>
    public (string BladePart, string Bit)? SplitTrailingBit(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var take = Math.Min(_maxBitWords, tokens.Length); take >= 1; take--)
        {
            var candidate = string.Join("", tokens[^take..]);
            if (_bitCanonical.TryGetValue(Normalize(candidate), out var canonical))
            {
                var bladePart = string.Join(' ', tokens[..^take]);
                if (bladePart.Length > 0)
                    return (bladePart, canonical);
            }
        }
        return null;
    }

    public static PartsVocabulary CreateDefault() => new(
        bits:
        [
            "Accel", "Ball", "Bound Spike", "Cyclone", "Disk Ball", "Dot", "Elevate",
            "Flat", "Free Ball", "Free Flat", "Gear Ball", "Gear Flat", "Gear Needle",
            "Gear Point", "Gear Rush", "Gear Unite", "Glide", "Hexa", "High Needle",
            "High Taper", "Jolt", "Kick", "Level", "Low Flat", "Low Orb", "Low Rush",
            "Merge", "Metal Needle", "Narrow", "Needle", "Orb", "Point", "Quake",
            "Rubber Accel", "Rush", "Spike", "Taper", "Trans Kick", "Trans Point",
            "Under Flat", "Under Needle", "Unite", "Vortex", "Wall Ball", "Wall Wedge",
            "Wedge", "Wide Ball", "Yielding", "Zap",
        ],
        assistBlades:
        [
            "OuterWheel", "Heavy", "Bumper", "Wheel", "Assault", "Round", "Massive",
        ]);
}
