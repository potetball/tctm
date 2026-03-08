namespace TCTM.Server.DataModel;

public enum TournamentFormat
{
    RoundRobin,
    Swiss,
    SingleElimination,
    DoubleElimination
}

public enum TimeControlPreset
{
    Bullet,
    Blitz,
    Rapid
}

public enum TournamentStatus
{
    Lobby,
    InProgress,
    Completed
}

public enum RoundStatus
{
    Pending,
    InProgress,
    Completed
}

public enum MatchResult
{
    WhiteWin,
    BlackWin,
    Draw
}

public enum Bracket
{
    Winners,
    Losers
}

public enum LiveGameStatus
{
    NotStarted,
    InProgress,
    Completed,
    Aborted
}
