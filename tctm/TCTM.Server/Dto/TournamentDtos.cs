using TCTM.Server.DataModel;

namespace TCTM.Server.Dto;

// POST /api/tournaments
public record CreateTournamentRequest(
    string Name,
    TournamentFormat Format,
    TimeControlPreset TimeControlPreset,
    int TimeControlMinutes,
    bool PlayBothColors = false
);

// Response after creating a tournament (includes secrets)
public record CreateTournamentResponse(
    string Slug,
    string Name,
    string InviteCode,
    string AdminToken
);

// POST /api/tournaments/{slug}/join
public record JoinTournamentRequest(
    string InviteCode,
    string DisplayName
);

// Response after joining a tournament
public record JoinTournamentResponse(
    Guid PlayerId,
    string PlayerToken
);

// GET /api/tournaments/{slug}
public record TournamentDto(
    Guid Id,
    string Slug,
    string Name,
    string InviteCode,
    TournamentFormat Format,
    TimeControlPreset TimeControlPreset,
    int TimeControlMinutes,
    bool PlayBothColors,
    TournamentStatus Status,
    DateTime CreatedAt,
    int PlayerCount
);

// POST /api/tournaments/reauthenticate
public record ReauthenticateRequest(
    string PlayerToken
);

public record ReauthenticateResponse(
    string Slug,
    Guid PlayerId,
    string DisplayName
);
