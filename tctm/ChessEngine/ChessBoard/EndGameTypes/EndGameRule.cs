namespace ChessEngine;

/// <summary>
/// https://www.chessprogramming.org/Draw
/// </summary>
internal abstract class EndGameRule
{
    protected ChessBoard board;

    internal abstract EndgameType Type { get; }

    internal EndGameRule(ChessBoard board)
    {
        this.board = board;
    }

    internal abstract bool IsEndGame();
}
