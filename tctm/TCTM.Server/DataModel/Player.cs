namespace TCTM.Server.DataModel;

public class Player
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public required string DisplayName { get; set; }
    public required string PlayerToken { get; set; }
    public int? Seed { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public ICollection<Match> WhiteMatches { get; set; } = [];
    public ICollection<Match> BlackMatches { get; set; } = [];
}
