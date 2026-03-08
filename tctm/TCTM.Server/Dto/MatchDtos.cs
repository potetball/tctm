using TCTM.Server.DataModel;

namespace TCTM.Server.Dto;

// Nested inside RoundDto and also used standalone
public record MatchDto(
    Guid Id,
    Guid RoundId,
    Guid? WhitePlayerId,
    string? WhitePlayerName,
    Guid? BlackPlayerId,
    string? BlackPlayerName,
    MatchResult? Result,
    Guid? ReportedBy,
    bool Disputed,
    Bracket? Bracket
);

// POST /api/tournaments/{slug}/matches/{id}/result
public record ReportResultRequest(
    MatchResult Result,
    string Token
);

// PUT /api/tournaments/{slug}/matches/{id}/result  (admin override)
public record OverrideResultRequest(
    MatchResult Result,
    string AdminToken
);
