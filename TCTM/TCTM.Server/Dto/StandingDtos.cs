namespace TCTM.Server.Dto;

// GET /api/tournaments/{slug}/standings
public record StandingDto(
    Guid PlayerId,
    string DisplayName,
    double Points,
    int Wins,
    int Draws,
    int Losses,
    double Buchholz,
    double SonnebornBerger
);
