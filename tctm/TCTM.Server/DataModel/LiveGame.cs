namespace TCTM.Server.DataModel;

public class LiveGame
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public long WhiteClockMs { get; set; }
    public long BlackClockMs { get; set; }
    public long InitialClockMs { get; set; }
    public string MoveData { get; set; } = string.Empty;
    public LiveGameStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Match Match { get; set; } = null!;
}
