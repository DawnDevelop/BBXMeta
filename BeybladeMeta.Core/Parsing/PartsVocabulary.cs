using System.Text.Json;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Canonical part names, matched case- and space-insensitively so that
/// "Wizard Rod", "WizardRod" and "wizardrod" all resolve to the same entry.
/// The seed below covers the parts seen in sample posts plus common releases;
/// the full list is meant to be synced from the Beyblade Wiki and loaded via FromJson.
/// </summary>
public sealed class PartsVocabulary
{
    private readonly Dictionary<string, string> _blades;   // normalized -> canonical
    private readonly Dictionary<string, string> _assists;
    private readonly Dictionary<string, string> _bits;
    private readonly List<string> _bladeKeysByLengthDesc;

    public PartsVocabulary(IEnumerable<string> blades, IEnumerable<string> assistBlades, IEnumerable<string> bits)
    {
        _blades = ToLookup(blades);
        _assists = ToLookup(assistBlades);
        _bits = ToLookup(bits);
        _bladeKeysByLengthDesc = _blades.Keys.OrderByDescending(k => k.Length).ToList();
    }

    public static string Normalize(string s) => s.Replace(" ", "").Replace("’", "'").ToLowerInvariant().Trim();

    private static Dictionary<string, string> ToLookup(IEnumerable<string> names) =>
        names.Distinct().ToDictionary(Normalize, n => n);

    /// <summary>Longest canonical blade whose normalized name is a prefix of <paramref name="normalizedText"/>.</summary>
    public (string Canonical, string Remainder)? MatchBladePrefix(string normalizedText)
    {
        foreach (var key in _bladeKeysByLengthDesc)
        {
            if (normalizedText.StartsWith(key, StringComparison.Ordinal))
                return (_blades[key], normalizedText[key.Length..]);
        }
        return null;
    }

    public string? MatchAssist(string normalizedText) =>
        _assists.TryGetValue(normalizedText, out var canonical) ? canonical : null;

    public string? MatchBit(string normalizedText) =>
        _bits.TryGetValue(normalizedText, out var canonical) ? canonical : null;

    public static PartsVocabulary FromJson(Stream json)
    {
        var doc = JsonSerializer.Deserialize<VocabularyJson>(json, JsonOptions)
                  ?? throw new InvalidDataException("Empty parts vocabulary JSON.");
        return new PartsVocabulary(doc.Blades, doc.AssistBlades ?? [], doc.Bits);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record VocabularyJson(List<string> Blades, List<string>? AssistBlades, List<string> Bits);

    public static PartsVocabulary CreateDefault() => new(
        blades:
        [
            // From observed thread posts
            "MeteorDragoon", "SharkScale", "WizardRod", "CobaltDragoon", "Valor Bison",
            "EmperorWhip", "AeroPegasus",
            // Common BBX releases
            "DranSword", "DranDagger", "DranBuster", "DranBrave",
            "HellsScythe", "HellsChain", "HellsHammer",
            "WizardArrow", "KnightShield", "KnightLance", "KnightMail",
            "SharkEdge", "PhoenixWing", "PhoenixFeather", "PhoenixRudder",
            "CobaltDrake", "ViperTail", "RhinoHorn", "UnicornSting",
            "SphinxCowl", "TyrannoBeat", "WhaleWave", "PteraSwing",
            "ShelterDrake", "BlackShell", "WeissTiger", "CrimsonGaruda",
            "ShinobiShadow", "GhostCircle", "LeonClaw", "ScorpioSpear",
            "SilverWolf", "SamuraiSaber", "KnifeShinobi", "BearScratch",
            "FoxBrush", "TuskMammoth", "HoverWyvern", "YellowDragster",
            "ImpactDrake", "TalonPtera", "SavageBear",
        ],
        assistBlades:
        [
            // From observed thread posts; CX assist parts sit between blade and ratchet
            "OuterWheel",
        ],
        bits:
        [
            // From observed thread posts
            "Level", "Rush", "Hexa", "Elevate", "Glide", "Unite", "Free Ball",
            // Common BBX bits
            "Flat", "Ball", "Point", "Needle", "Taper", "High Taper", "Low Flat",
            "Orb", "Dot", "Quake", "Spike", "Cyclone", "Accel", "Vortex",
            "Gear Flat", "Gear Ball", "Gear Point", "Gear Needle", "Gear Rush",
            "Metal Needle", "Bound Spike", "Disk Ball", "Trans Point",
        ]);
}
