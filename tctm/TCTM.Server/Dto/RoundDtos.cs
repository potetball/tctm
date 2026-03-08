using TCTM.Server.DataModel;

namespace TCTM.Server.Dto;

// GET /api/tournaments/{slug}/rounds
public record RoundDto(
    Guid Id,
    int RoundNumber,
    RoundStatus Status,
    List<MatchDto> Matches
);
