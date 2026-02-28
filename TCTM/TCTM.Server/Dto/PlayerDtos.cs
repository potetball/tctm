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
