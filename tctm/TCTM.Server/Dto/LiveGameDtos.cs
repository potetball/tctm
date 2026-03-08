using TCTM.Server.DataModel;

namespace TCTM.Server.Dto;

/// <summary>
/// GET response for a live game.
/// </summary>
public record LiveGameDto(
    Guid Id,
    Guid MatchId,
    PlayerSummaryDto? WhitePlayer,
    PlayerSummaryDto? BlackPlayer,
    long WhiteClockMs,
    long BlackClockMs,
    long InitialClockMs,
    string MoveData,
    LiveGameStatus Status,
    string? CurrentFen,
    int PlyCount,
    DateTime? StartedAt,
    DateTime? CompletedAt
);

/// <summary>
/// Minimal player info included in LiveGameDto.
/// </summary>
public record PlayerSummaryDto(
    Guid Id,
    string DisplayName
);

/// <summary>
/// Item in the list of live games for a tournament.
/// </summary>
public record LiveGameSummaryDto(
    Guid Id,
    Guid MatchId,
    string? WhitePlayerName,
    string? BlackPlayerName,
    LiveGameStatus Status,
    int PlyCount,
    DateTime? StartedAt,
    DateTime? CompletedAt
);

/// <summary>
/// POST body for aborting a live game.
/// </summary>
public record AbortLiveGameRequest(
    string AdminToken
);
