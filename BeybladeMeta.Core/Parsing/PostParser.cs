using System.Text.RegularExpressions;
using BeybladeMeta.Core.Models;

namespace BeybladeMeta.Core.Parsing;

/// <summary>
/// Parses the plain text of one forum post into placement blocks (1st/2nd/3rd)
/// and dictionary-matches each combo line against the parts vocabulary.
/// Lines inside a block that fail to match are reported as unmatched, never guessed.
/// </summary>
public sealed partial class PostParser(PartsVocabulary vocabulary)
{
    [GeneratedRegex(@"^\s*(?<num>\d+)(st|nd|rd|th)\b(\s+place)?\s*[:\-–—]?\s*(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlacementMarker();

    [GeneratedRegex(@"(?<ratchet>\d{1,2}-\d{2,3})")]
    private static partial Regex RatchetPattern();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthetical();

    public ParsedPost Parse(string postText)
    {
        var placements = new List<PlacementResult>();
        var unmatched = new List<UnmatchedLine>();

        int currentPlacement = 0; // 0 = outside any tracked block
        string currentPlayer = "";
        var currentCombos = new List<Combo>();

        void FlushBlock()
        {
            if (currentPlacement is >= 1 and <= 3 && currentCombos.Count > 0)
                placements.Add(new PlacementResult(currentPlacement, currentPlayer, currentCombos.ToList()));
            currentCombos.Clear();
        }

        foreach (var rawLine in postText.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var marker = PlacementMarker().Match(line);
            if (marker.Success)
            {
                FlushBlock();
                var num = int.Parse(marker.Groups["num"].Value);
                currentPlacement = num is >= 1 and <= 3 ? num : 0;
                currentPlayer = CleanPlayerName(marker.Groups["rest"].Value);
                continue;
            }

            if (currentPlacement == 0)
                continue; // prose before "1st" or after "4th"

            if (line.EndsWith(':'))
                continue; // section headers like "Final Stage - Registered Deck List:"

            var combo = TryParseComboLine(line);
            if (combo is not null)
                currentCombos.Add(combo);
            else
                unmatched.Add(new UnmatchedLine(currentPlacement, line));
        }

        FlushBlock();
        return new ParsedPost(placements, unmatched);
    }

    private static string CleanPlayerName(string rest) =>
        rest.Trim().TrimStart('@').Trim('"', '\'', ' ');

    private Combo? TryParseComboLine(string line)
    {
        var cleaned = Parenthetical().Replace(line, "").Trim().TrimEnd('.', ',', ';');
        if (cleaned.Length == 0)
            return null;

        var ratchetMatch = RatchetPattern().Match(cleaned);
        if (ratchetMatch.Success)
        {
            var blade = MatchBladeChain(cleaned[..ratchetMatch.Index]);
            var bit = vocabulary.MatchBit(PartsVocabulary.Normalize(cleaned[(ratchetMatch.Index + ratchetMatch.Length)..]));
            return blade is not null && bit is not null
                ? new Combo(blade.Value.Blade, blade.Value.Assist, ratchetMatch.Groups["ratchet"].Value, bit)
                : null;
        }

        // No ratchet code: blade + bit only (e.g. "Valor Bison Glide")
        var normalized = PartsVocabulary.Normalize(cleaned);
        if (vocabulary.MatchBladePrefix(normalized) is not var (canonicalBlade, remainder))
            return null;
        var soloBit = vocabulary.MatchBit(remainder);
        return soloBit is not null ? new Combo(canonicalBlade, null, null, soloBit) : null;
    }

    private (string Blade, string? Assist)? MatchBladeChain(string text)
    {
        var normalized = PartsVocabulary.Normalize(text);
        if (normalized.Length == 0)
            return null;
        if (vocabulary.MatchBladePrefix(normalized) is not var (blade, remainder))
            return null;
        if (remainder.Length == 0)
            return (blade, null);
        var assist = vocabulary.MatchAssist(remainder);
        return assist is not null ? (blade, assist) : null;
    }
}
