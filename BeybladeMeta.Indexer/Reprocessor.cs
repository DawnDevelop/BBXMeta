using System.Text.Json;
using BeybladeMeta.Core.Models;
using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Rebuilds the JSON exports with the current parser — re-parsing recovers combos
/// and merges bit forms without re-scraping. Existing dates are carried through.
/// </summary>
public static class Reprocessor
{
    private sealed record OldAppearance(string? Blade, string Display, int Placement, string? Date);
    private sealed record OldUnmatched(int Page, int Placement, string Line);
    private sealed record NewAppearance(string Blade, string Display, int Placement, string? Date);

    public static void Run(string dir)
    {
        var parser = new PostParser(PartsVocabulary.CreateDefault());
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var outOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var appPath = Path.Combine(dir, "appearances.json");
        var unmPath = Path.Combine(dir, "unmatched.json");
        var oldApp = JsonSerializer.Deserialize<List<OldAppearance>>(File.ReadAllText(appPath), opts) ?? [];
        var oldUnm = JsonSerializer.Deserialize<List<OldUnmatched>>(File.ReadAllText(unmPath), opts) ?? [];

        var parsed = new List<(Combo Combo, int Placement, string? Date)>();
        var stillUnmatched = new List<OldUnmatched>();

        // Previously-matched combos: re-parse their display so parts are re-derived consistently.
        foreach (var a in oldApp)
        {
            var combo = parser.TryParseCombo(a.Display);
            if (combo is not null)
                parsed.Add((combo, a.Placement, a.Date));
        }

        // Previously-unmatched raw lines: recover the ones the new parser now understands.
        int recovered = 0;
        foreach (var u in oldUnm)
        {
            var combo = parser.TryParseCombo(u.Line);
            if (combo is not null)
            {
                parsed.Add((combo, u.Placement, null));
                recovered++;
            }
            else if (LooksLikeCombo(parser, u.Line))
            {
                stillUnmatched.Add(u);
            }
            // else prose/noise — drop
        }

        // Merge spelling variants, group CX blades by main blade, then rebuild displays.
        var bladeMap = BladeCanonicalizer.BuildMap(parsed.Select(p => p.Combo.Blade));
        var appearances = parsed.Select(p =>
        {
            var (groupBlade, displayBladePart) = CxSystem.Resolve(bladeMap[p.Combo.Blade]);
            var display = new Combo(displayBladePart, p.Combo.AssistBlade, p.Combo.Ratchet, p.Combo.Bit).Display;
            return new NewAppearance(groupBlade, display, p.Placement, p.Date);
        }).ToList();

        File.WriteAllText(appPath, JsonSerializer.Serialize(appearances, outOpts));
        File.WriteAllText(unmPath, JsonSerializer.Serialize(stillUnmatched, outOpts));
        var dated = appearances.Count(a => a.Date is not null);
        Console.WriteLine($"Reprocessed: {appearances.Count} appearances ({recovered} recovered from unmatched, " +
                          $"{dated} dated), {stillUnmatched.Count} still unmatched.");
    }

    // Keep genuine combo-looking misses in the review list; drop prose entirely.
    private static bool LooksLikeCombo(PostParser parser, string line) =>
        System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{1,2}-\d{2,3}");
}
