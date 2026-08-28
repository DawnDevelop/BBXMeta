namespace BeybladeMeta.Core.Models;

public sealed record PlacementResult(int Placement, IReadOnlyList<Combo> Combos);

public sealed record UnmatchedLine(int Placement, string Line);

public sealed record ParsedPost(IReadOnlyList<PlacementResult> Placements, IReadOnlyList<UnmatchedLine> Unmatched);
