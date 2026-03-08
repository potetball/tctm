using ChessEngine;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Services;

namespace TCTM.Server.Mappings;

public static class LiveGameMappings
{
    public static LiveGameDto ToDto(this LiveGame liveGame)
    {
        var match = liveGame.Match;

        string? currentFen = null;
        var moveTokens = MoveDataParser.ParseMoves(liveGame.MoveData);
        int plyCount = moveTokens.Count;

        if (plyCount > 0)
        {
            try
            {
                var board = new ChessBoard { AutoEndgameRules = AutoEndgameRules.All };
                foreach (var token in moveTokens)
                {
                    board.Move(token.San);
                }
                currentFen = board.ToFen();
            }
            catch
            {
                // If FEN generation fails, leave null
            }
        }
        else
        {
            currentFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        }

        return new LiveGameDto(
            liveGame.Id,
            liveGame.MatchId,
            match?.WhitePlayer is not null
                ? new PlayerSummaryDto(match.WhitePlayer.Id, match.WhitePlayer.DisplayName)
                : null,
            match?.BlackPlayer is not null
                ? new PlayerSummaryDto(match.BlackPlayer.Id, match.BlackPlayer.DisplayName)
                : null,
            liveGame.WhiteClockMs,
            liveGame.BlackClockMs,
            liveGame.InitialClockMs,
            liveGame.MoveData,
            liveGame.Status,
            currentFen,
            plyCount,
            liveGame.StartedAt,
            liveGame.CompletedAt
        );
    }

    public static LiveGameSummaryDto ToSummaryDto(this LiveGame liveGame)
    {
        var match = liveGame.Match;
        var plyCount = MoveDataParser.ParseMoves(liveGame.MoveData).Count;

        return new LiveGameSummaryDto(
            liveGame.Id,
            liveGame.MatchId,
            match?.WhitePlayer?.DisplayName,
            match?.BlackPlayer?.DisplayName,
            liveGame.Status,
            plyCount,
            liveGame.StartedAt,
            liveGame.CompletedAt
        );
    }
}
