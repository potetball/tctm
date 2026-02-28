namespace TCTM.Server.DataModel;

public class Tournament
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string InviteCode { get; set; }
    public required string AdminToken { get; set; }
    public TournamentFormat Format { get; set; }
    public TimeControlPreset TimeControlPreset { get; set; }
    public int TimeControlMinutes { get; set; }
    public bool PlayBothColors { get; set; }
    public TournamentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Player> Players { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
    public ICollection<Standing> Standings { get; set; } = [];
}
