namespace TCTM.Server.DataModel;

public class Round
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public int RoundNumber { get; set; }
    public RoundStatus Status { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = [];
}
