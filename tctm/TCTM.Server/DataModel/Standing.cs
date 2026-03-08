namespace TCTM.Server.DataModel;

public class Standing
{
    public Guid TournamentId { get; set; }
    public Guid PlayerId { get; set; }
    public double Points { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double Buchholz { get; set; }
    public double SonnebornBerger { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
