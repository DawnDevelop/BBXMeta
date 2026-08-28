namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Beyblade X combos are parsed structurally (anchored on the ratchet code), so
/// no blade catalog is needed. This vocabulary only supports two narrow jobs:
/// splitting a CX assist part off the blade name, and recognizing ratchet-less
/// combos (unique blades like "BulletGriffon Hexa") by their trailing bit.
/// </summary>
public sealed class PartsVocabulary
{
    private readonly Dictionary<string, string> _bits; // normalized -> canonical (spaced) form
    private readonly HashSet<string> _assists;         // normalized assist-blade names
    private readonly int _maxBitWords;

    public PartsVocabulary(IEnumerable<string> bits, IEnumerable<string> assistBlades)
    {
        _bits = bits.Distinct().ToDictionary(Normalize, b => b);
        _assists = assistBlades.Select(Normalize).ToHashSet();
        _maxBitWords = _bits.Keys.Count == 0 ? 1 : bits.Select(b => b.Split(' ').Length).Max();
    }

    public static string Normalize(string s) =>
        s.Replace(" ", "").Replace(" ", "").ToLowerInvariant().Trim();

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
            if (_bits.TryGetValue(Normalize(candidate), out var canonical))
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
