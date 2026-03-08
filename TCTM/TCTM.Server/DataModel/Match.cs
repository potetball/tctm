namespace TCTM.Server.DataModel;

public class Match
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Guid? WhitePlayerId { get; set; }
    public Guid? BlackPlayerId { get; set; }
    public MatchResult? Result { get; set; }
    public Guid? ReportedBy { get; set; }
    public bool Disputed { get; set; }
    public Bracket? Bracket { get; set; }

    public Round Round { get; set; } = null!;
    public Player? WhitePlayer { get; set; }
    public Player? BlackPlayer { get; set; }
    public LiveGame? LiveGame { get; set; }
}
