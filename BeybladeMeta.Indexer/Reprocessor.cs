using System.Text.Json;
using BeybladeMeta.Core.Parsing;

namespace BeybladeMeta.Indexer;

/// <summary>
/// Rebuilds appearances.json/unmatched.json from the existing exports using the
/// current parser — recovers combos the old parser missed without re-scraping.
/// Post dates are not in the exports, so weeks stay empty until a real re-index.
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

        var appearances = new List<NewAppearance>();
        var stillUnmatched = new List<OldUnmatched>();

        // Previously-matched combos: re-parse their display so blades are re-derived consistently.
        foreach (var a in oldApp)
        {
            var combo = parser.TryParseCombo(a.Display);
            if (combo is not null)
                appearances.Add(new NewAppearance(combo.Blade, combo.Display, a.Placement, a.Date));
        }

        // Previously-unmatched raw lines: recover the ones the new parser now understands.
        int recovered = 0;
        foreach (var u in oldUnm)
        {
            var combo = parser.TryParseCombo(u.Line);
            if (combo is not null)
            {
                appearances.Add(new NewAppearance(combo.Blade, combo.Display, u.Placement, null));
                recovered++;
            }
            else if (LooksLikeCombo(parser, u.Line))
            {
                stillUnmatched.Add(u);
            }
            // else prose/noise — drop
        }

        File.WriteAllText(appPath, JsonSerializer.Serialize(appearances, outOpts));
        File.WriteAllText(unmPath, JsonSerializer.Serialize(stillUnmatched, outOpts));
        Console.WriteLine($"Reprocessed: {appearances.Count} appearances ({recovered} recovered from unmatched), " +
                          $"{stillUnmatched.Count} still unmatched. Weeks are null until a re-index.");
    }

    // Keep genuine combo-looking misses in the review list; drop prose entirely.
    private static bool LooksLikeCombo(PostParser parser, string line) =>
        System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{1,2}-\d{2,3}");
}
