using System.Text.RegularExpressions;
using BeybladeMeta.Core.Models;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Parses a forum post into 1st/2nd/3rd placement blocks and their combos, anchored
/// on the ratchet code so no blade catalog is needed. Player names are discarded.
/// </summary>
public sealed partial class PostParser(PartsVocabulary vocabulary)
{
    [GeneratedRegex(@"^\s*(?<num>\d+)(st|nd|rd|th)\b(\s+place)?\s*[:\-–—]?", RegexOptions.IgnoreCase)]
    private static partial Regex PlacementMarker();

    [GeneratedRegex(@"\d{1,2}-\d{2,3}")]
    private static partial Regex RatchetPattern();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthetical();

    public ParsedPost Parse(string postText)
    {
        var placements = new List<PlacementResult>();
        var unmatched = new List<UnmatchedLine>();

        int currentPlacement = 0; // 0 = outside any tracked block
        var currentCombos = new List<Combo>();

        void FlushBlock()
        {
            if (currentPlacement is >= 1 and <= 3 && currentCombos.Count > 0)
                placements.Add(new PlacementResult(currentPlacement, currentCombos.ToList()));
            currentCombos.Clear();
        }

        foreach (var rawLine in postText.Split('\n'))
        {
            var line = rawLine.Replace(' ', ' ').Trim().TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;

            var marker = PlacementMarker().Match(line);
            if (marker.Success)
            {
                FlushBlock();
                var num = int.Parse(marker.Groups["num"].Value);
                currentPlacement = num is >= 1 and <= 3 ? num : 0;
                continue; // remainder of the line is the player name — ignored
            }

            if (currentPlacement == 0)
                continue;

            switch (TryParseComboLine(line))
            {
                case { } combo:
                    currentCombos.Add(combo);
                    break;
                case null when LooksLikeComboAttempt(line):
                    unmatched.Add(new UnmatchedLine(currentPlacement, line));
                    break;
                // otherwise prose/noise — skip silently
            }
        }

        FlushBlock();
        return new ParsedPost(placements, unmatched);
    }

    /// <summary>Parse a single combo line in isolation.</summary>
    public Combo? TryParseCombo(string line) => TryParseComboLine(line.Trim());

    private Combo? TryParseComboLine(string line)
    {
        var cleaned = Parenthetical().Replace(line, "").Trim();
        if (cleaned.Length == 0)
            return null;

        var ratchet = RatchetPattern().Match(cleaned);
        if (ratchet.Success)
        {
            var bladePart = cleaned[..ratchet.Index].Trim();
            var bit = CleanBit(cleaned[(ratchet.Index + ratchet.Length)..]);
            if (bladePart.Length == 0 || bit.Length == 0)
                return null;
            var (blade, assist) = SplitAssist(bladePart);
            return new Combo(blade, assist, ratchet.Value, vocabulary.CanonicalBit(bit));
        }

        // No ratchet: accept only if the line ends with a known bit (unique/CX blades).
        if (vocabulary.SplitTrailingBit(cleaned.TrimEnd('.', ',', ';')) is not var (bladePart2, bit2) || bladePart2 is null)
            return null;
        var (blade2, assist2) = SplitAssist(bladePart2);
        return new Combo(blade2, assist2, null, bit2);
    }

    private (string Blade, string? Assist) SplitAssist(string bladePart) => vocabulary.SplitAssist(bladePart);

    // Strip trailing annotations like "- Finals", ", First & Final", stray brackets.
    private static string CleanBit(string bit)
    {
        bit = bit.Split(" - ", 2)[0];
        bit = bit.Split(',', 2)[0];
        return bit.Trim().Trim('[', ']', '(', ')', '"', '\'', '.', ' ');
    }

    // A combo attempt has a ratchet or ends with a known bit; other prose isn't flagged.
    private bool LooksLikeComboAttempt(string line)
    {
        var cleaned = Parenthetical().Replace(line, "").Trim();
        return RatchetPattern().IsMatch(cleaned)
               || vocabulary.SplitTrailingBit(cleaned.TrimEnd('.', ',', ';')) is not null;
    }
}
