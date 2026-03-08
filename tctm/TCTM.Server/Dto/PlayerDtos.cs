namespace TCTM.Server.Dto;

// GET /api/tournaments/{slug}/players
public record PlayerDto(
    Guid Id,
    string DisplayName,
    int? Seed
);

// PUT /api/tournaments/{slug}/players/seed
public record SetSeedOrderRequest(
    List<Guid> PlayerIds
);

// POST /api/tournaments/{slug}/players/{id}/reset-token
public record ResetTokenResponse(
    Guid PlayerId,
    string DisplayName,
    string PlayerToken
);
