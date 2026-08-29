namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Supports the parser's non-structural needs: splitting a CX assist part off the
/// blade, recognizing ratchet-less combos by their trailing bit, and canonicalizing
/// bit names so abbreviations merge with full names ("Fb"/"FreeBall" → "Free Ball").
/// </summary>
public sealed class PartsVocabulary
{
    private readonly Dictionary<string, string> _bitCanonical;    // normalized -> canonical bit
    private readonly Dictionary<string, string> _assistCanonical; // normalized (incl. code) -> canonical assist
    private readonly int _maxBitWords;

    public PartsVocabulary(IEnumerable<string> bits, IEnumerable<string> assistBlades)
    {
        var bitList = bits.Distinct().ToList();
        _bitCanonical = BuildBitMap(bitList);
        _assistCanonical = BuildAssistMap(assistBlades.Distinct().ToList());
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

    // Assists map their full name and single-letter code (Jaggy/J, Heavy/H) to the full name.
    // Codes are unambiguous among assists; bit codes live in a separate map and a separate
    // position (after the ratchet), so "H" as a blade-side assist never clashes with "H"=Hexa.
    private static Dictionary<string, string> BuildAssistMap(List<string> assists)
    {
        var map = new Dictionary<string, string>();
        foreach (var a in assists)
            map[Normalize(a)] = a;
        var byCode = assists.GroupBy(a => Normalize(a)[..1]);
        foreach (var group in byCode)
            if (group.Count() == 1 && !map.ContainsKey(group.Key))
                map[group.Key] = group.Single();
        return map;
    }

    /// <summary>Splits a trailing assist part (full name or code) off a blade string.</summary>
    public (string Blade, string? Assist) SplitAssist(string bladePart)
    {
        var tokens = bladePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2 && _assistCanonical.TryGetValue(Normalize(tokens[^1]), out var assist))
            return (string.Join(' ', tokens[..^1]), assist);
        return (bladePart, null);
    }

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
        assistBlades: CxSystem.AssistBlades);
}
