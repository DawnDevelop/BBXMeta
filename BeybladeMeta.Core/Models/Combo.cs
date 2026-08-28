namespace BeybladeMeta.Core.Models;

/// <summary>
/// A single Beyblade X combo. Ratchet is null for combos whose line carries no
/// ratchet code (e.g. "Valor Bison Glide"). AssistBlade covers CX-style lines
/// where an assist part sits between blade and ratchet.
/// </summary>
public sealed record Combo(string Blade, string? AssistBlade, string? Ratchet, string Bit)
{
    /// <summary>Canonical display form matching thread convention, e.g. "WizardRod 1-60Hexa".</summary>
    public string Display =>
        Ratchet is null
            ? $"{BladePart} {Bit}"
            : $"{BladePart} {Ratchet}{Bit.Replace(" ", "")}";

    private string BladePart => AssistBlade is null ? Blade : $"{Blade} {AssistBlade}";

    /// <summary>Stable key for aggregation (case/space-insensitive identity).</summary>
    public string Key => Display.Replace(" ", "").ToLowerInvariant();
}
