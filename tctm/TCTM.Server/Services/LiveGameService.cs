using ChessEngine;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Hubs;
using TCTM.Server.Mappings;

namespace TCTM.Server.Services;

/// <summary>
/// Core service for live game logic: move validation, clock management, game lifecycle,
/// and cross-hub notification.
/// </summary>
public class LiveGameService(
    TctmDbContext db,
    IHubContext<LiveGameHub> liveGameHub,
    IHubContext<TournamentHub> tournamentHub,
    ILogger<LiveGameService> logger)
{
    /// <summary>Clock tolerance in ms — if client diverges more than this, server value is used.</summary>
    private const long ClockToleranceMs = 2000;

    // ──────────────────────────────────────────────
    //  Get or auto-create
    // ──────────────────────────────────────────────

    /// <summary>
    /// Get an existing LiveGame for a match, or auto-create one if the caller is a participant.
    /// </summary>
    public async Task<LiveGame?> GetOrCreateAsync(Guid matchId, string slug, Guid? callerPlayerId)
    {
        var liveGame = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.WhitePlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.BlackPlayer)
            .FirstOrDefaultAsync(lg => lg.MatchId == matchId);

        if (liveGame is not null)
            return liveGame;

        // Auto-create only if caller is a participant
        if (callerPlayerId is null)
            return null;

        var match = await db.Matches
            .Include(m => m.WhitePlayer)
            .Include(m => m.BlackPlayer)
            .Include(m => m.Round)
                .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.Round.Tournament.Slug == slug);

        if (match is null)
            return null;

        if (match.WhitePlayerId != callerPlayerId && match.BlackPlayerId != callerPlayerId)
            return null;

        var initialClockMs = match.Round.Tournament.TimeControlMinutes * 60 * 1000L;

        liveGame = new LiveGame
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            WhiteClockMs = initialClockMs,
            BlackClockMs = initialClockMs,
            InitialClockMs = initialClockMs,
            MoveData = string.Empty,
            Status = LiveGameStatus.NotStarted,
        };

        db.LiveGames.Add(liveGame);
        await db.SaveChangesAsync();

        // Reload with navigation properties
        liveGame = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.WhitePlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.BlackPlayer)
            .FirstAsync(lg => lg.Id == liveGame.Id);

        return liveGame;
    }

    // ──────────────────────────────────────────────
    //  Start game
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> StartGameAsync(Guid liveGameId, Guid callerPlayerId, bool isAdmin)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.");

        if (liveGame.Status != LiveGameStatus.NotStarted)
            return (false, "Game is not in NotStarted state.");

        // Only Black player or admin can start the game
        if (!isAdmin && liveGame.Match.BlackPlayerId != callerPlayerId)
            return (false, "Only the Black player or an admin can start the game.");

        liveGame.Status = LiveGameStatus.InProgress;
        liveGame.StartedAt = DateTime.UtcNow;
        liveGame.WhiteClockMs = liveGame.InitialClockMs;
        liveGame.BlackClockMs = liveGame.InitialClockMs;

        await db.SaveChangesAsync();

        await liveGameHub.Clients.Group($"game:{liveGameId}").SendAsync("GameStarted", new
        {
            liveGameId,
            whiteClockMs = liveGame.WhiteClockMs,
            blackClockMs = liveGame.BlackClockMs,
        });

        return (true, null);
    }

    // ──────────────────────────────────────────────
    //  Submit move
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error, object? Broadcast)> SubmitMoveAsync(
        Guid liveGameId, string san, long clientClockMs, Guid callerPlayerId)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.", null);

        if (liveGame.Status != LiveGameStatus.InProgress)
            return (false, "Game is not in progress.", null);

        var match = liveGame.Match;
        bool isWhiteTurn = MoveDataParser.IsWhiteTurn(liveGame.MoveData);

        // Validate turn
        if (isWhiteTurn && match.WhitePlayerId != callerPlayerId)
            return (false, "It is not your turn.", null);
        if (!isWhiteTurn && match.BlackPlayerId != callerPlayerId)
            return (false, "It is not your turn.", null);

        // Build board from existing moves to validate the new move
        var board = ReplayBoard(liveGame.MoveData);
        if (board is null)
            return (false, "Failed to reconstruct board state.", null);

        // Validate SAN move legality
        if (!board.IsValidMove(san))
            return (false, $"Illegal move: {san}", null);

        // Execute the move on the board
        bool moved = board.Move(san);
        if (!moved)
            return (false, $"Move execution failed: {san}", null);

        // Clock management
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tokens = MoveDataParser.Parse(liveGame.MoveData);
        var moveTokens = tokens.Where(t => !MoveDataParser.IsControlToken(t)).ToList();
        int currentPly = moveTokens.Count;
        int newPly = currentPly + 1;

        long serverClockMs;
        if (moveTokens.Count > 0)
        {
            var lastMove = moveTokens[^1];
            long elapsed = now - lastMove.EpochMs;
            long previousClock = isWhiteTurn ? liveGame.WhiteClockMs : liveGame.BlackClockMs;
            long expectedClock = previousClock - elapsed;

            // Use client value if within tolerance, otherwise use server calculation
            if (Math.Abs(clientClockMs - expectedClock) <= ClockToleranceMs)
                serverClockMs = clientClockMs;
            else
                serverClockMs = Math.Max(0, expectedClock);
        }
        else
        {
            // First move — clock hasn't really ticked yet (or minimal time)
            serverClockMs = clientClockMs;
        }

        if (serverClockMs < 0) serverClockMs = 0;

        // Check timeout: if clock expired, record timeout instead
        if (serverClockMs <= 0)
        {
            return await HandleTimeoutAsync(liveGame, isWhiteTurn, now);
        }

        // Update clock
        if (isWhiteTurn)
            liveGame.WhiteClockMs = serverClockMs;
        else
            liveGame.BlackClockMs = serverClockMs;

        // Append move token
        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, newPly, san, serverClockMs, now);

        // Check for game-ending conditions
        string newFen = board.ToFen();

        if (board.IsEndGame)
        {
            await HandleBoardEndGameAsync(liveGame, board, now);
        }

        await db.SaveChangesAsync();

        var broadcastPayload = new
        {
            liveGameId,
            token = $"{newPly}:{san}:{serverClockMs}:{now}",
            fen = newFen,
        };

        await liveGameHub.Clients.Group($"game:{liveGameId}").SendAsync("MovePlayed", broadcastPayload);

        // If game ended, also broadcast via TournamentHub
        if (liveGame.Status == LiveGameStatus.Completed)
        {
            await BroadcastGameEndedAsync(liveGame);
        }

        return (true, null, broadcastPayload);
    }

    // ──────────────────────────────────────────────
    //  Resign
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> ResignAsync(Guid liveGameId, Guid callerPlayerId)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.");

        if (liveGame.Status != LiveGameStatus.InProgress)
            return (false, "Game is not in progress.");

        var match = liveGame.Match;
        bool isWhite = match.WhitePlayerId == callerPlayerId;
        bool isBlack = match.BlackPlayerId == callerPlayerId;
        if (!isWhite && !isBlack) return (false, "You are not a participant in this game.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int ply = MoveDataParser.CurrentPly(liveGame.MoveData) + 1;
        long clockMs = isWhite ? liveGame.WhiteClockMs : liveGame.BlackClockMs;

        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, ply, "resign", clockMs, now);
        liveGame.Status = LiveGameStatus.Completed;
        liveGame.CompletedAt = DateTime.UtcNow;

        // Set match result: the resigning player loses
        match.Result = isWhite ? MatchResult.BlackWin : MatchResult.WhiteWin;

        await db.SaveChangesAsync();

        await BroadcastGameEndedAsync(liveGame, "resignation");

        return (true, null);
    }

    // ──────────────────────────────────────────────
    //  Draw offer / accept
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> OfferDrawAsync(Guid liveGameId, Guid callerPlayerId)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.");

        if (liveGame.Status != LiveGameStatus.InProgress)
            return (false, "Game is not in progress.");

        var match = liveGame.Match;
        bool isWhiteTurn = MoveDataParser.IsWhiteTurn(liveGame.MoveData);

        // A draw can only be offered when it is the offering player's turn
        bool isWhite = match.WhitePlayerId == callerPlayerId;
        bool isBlack = match.BlackPlayerId == callerPlayerId;
        if (!isWhite && !isBlack) return (false, "You are not a participant in this game.");

        if (isWhite && !isWhiteTurn) return (false, "You can only offer a draw on your turn.");
        if (isBlack && isWhiteTurn) return (false, "You can only offer a draw on your turn.");

        if (MoveDataParser.HasPendingDrawOffer(liveGame.MoveData))
            return (false, "A draw offer is already pending.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int ply = MoveDataParser.CurrentPly(liveGame.MoveData) + 1;
        long clockMs = isWhite ? liveGame.WhiteClockMs : liveGame.BlackClockMs;

        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, ply, "draw-offer", clockMs, now);

        await db.SaveChangesAsync();

        string offeredBy = isWhite ? "white" : "black";
        await liveGameHub.Clients.Group($"game:{liveGameId}").SendAsync("DrawOffered", new
        {
            liveGameId,
            offeredBy,
        });

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AcceptDrawAsync(Guid liveGameId, Guid callerPlayerId)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.");

        if (liveGame.Status != LiveGameStatus.InProgress)
            return (false, "Game is not in progress.");

        if (!MoveDataParser.HasPendingDrawOffer(liveGame.MoveData))
            return (false, "No pending draw offer.");

        var match = liveGame.Match;

        // The accepting player must be the *opponent* of the player who offered
        // The draw-offer token was placed by the player whose turn it was when offered.
        // Since draw-offer is appended after the actual chess moves, we need to figure out who offered.
        var allTokens = MoveDataParser.Parse(liveGame.MoveData);
        var drawOfferToken = allTokens.Last(t => t.San == "draw-offer");
        // The ply in the draw-offer token tells us whose turn it was (odd = white's turn, even = black's turn)
        int movePly = MoveDataParser.ParseMoves(liveGame.MoveData).Count;
        // Actually, we stored draw-offer with ply = currentPly + 1.
        // The draw offer ply follows the chess-move count. Since draw-offer is offered on the player's turn:
        // If chess ply count is even (white's turn), ply stored = even+1 = odd → offered by white
        // If chess ply count is odd (black's turn), ply stored = odd+1 = even → offered by black
        // Simpler: check which player's ID we need to verify as the *accepting* party.
        // Let's use the move count at the time of draw-offer: find how many chess moves are before the draw-offer token
        var tokensBeforeDrawOffer = allTokens.TakeWhile(t => t != drawOfferToken).ToList();
        int chessPlyBeforeOffer = tokensBeforeDrawOffer.Count(t => !MoveDataParser.IsControlToken(t));
        bool offeredByWhite = chessPlyBeforeOffer % 2 == 0; // white's turn when even ply count

        bool isWhite = match.WhitePlayerId == callerPlayerId;
        bool isBlack = match.BlackPlayerId == callerPlayerId;
        if (!isWhite && !isBlack) return (false, "You are not a participant in this game.");

        // Acceptor must be the opponent
        if (offeredByWhite && isWhite) return (false, "You cannot accept your own draw offer.");
        if (!offeredByWhite && isBlack) return (false, "You cannot accept your own draw offer.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int ply = MoveDataParser.CurrentPly(liveGame.MoveData) + 1;
        long clockMs = isWhite ? liveGame.WhiteClockMs : liveGame.BlackClockMs;

        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, ply, "draw-accept", clockMs, now);
        liveGame.Status = LiveGameStatus.Completed;
        liveGame.CompletedAt = DateTime.UtcNow;
        match.Result = MatchResult.Draw;

        await db.SaveChangesAsync();

        await BroadcastGameEndedAsync(liveGame, "draw agreement");

        return (true, null);
    }

    // ──────────────────────────────────────────────
    //  Abort
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AbortGameAsync(Guid liveGameId)
    {
        var liveGame = await LoadGameAsync(liveGameId);
        if (liveGame is null) return (false, "Live game not found.");

        if (liveGame.Status == LiveGameStatus.Completed || liveGame.Status == LiveGameStatus.Aborted)
            return (false, "Game is already finished.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int ply = MoveDataParser.CurrentPly(liveGame.MoveData) + 1;

        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, ply, "abort", 0, now);
        liveGame.Status = LiveGameStatus.Aborted;
        liveGame.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await liveGameHub.Clients.Group($"game:{liveGameId}").SendAsync("GameAborted", new { liveGameId });

        // Notify tournament
        var slug = await GetTournamentSlugAsync(liveGame);
        if (slug is not null)
        {
            await tournamentHub.Clients.Group(slug).SendAsync("GameAborted", new { liveGameId });
        }

        return (true, null);
    }

    // ──────────────────────────────────────────────
    //  Timeout (called by ClockMonitorService)
    // ──────────────────────────────────────────────

    public async Task CheckTimeoutsAsync()
    {
        var activeGames = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.WhitePlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.BlackPlayer)
            .Where(lg => lg.Status == LiveGameStatus.InProgress)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var liveGame in activeGames)
        {
            bool isWhiteTurn = MoveDataParser.IsWhiteTurn(liveGame.MoveData);
            var tokens = MoveDataParser.Parse(liveGame.MoveData);
            var moveTokens = tokens.Where(t => !MoveDataParser.IsControlToken(t)).ToList();

            if (moveTokens.Count == 0) continue; // No moves yet, clock hasn't started ticking

            var lastMoveToken = moveTokens[^1];
            long elapsed = now - lastMoveToken.EpochMs;

            long remainingClock = isWhiteTurn ? liveGame.WhiteClockMs : liveGame.BlackClockMs;
            long expectedRemaining = remainingClock - elapsed;

            if (expectedRemaining <= 0)
            {
                await HandleTimeoutAsync(liveGame, isWhiteTurn, now);
                await db.SaveChangesAsync();
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Clock update broadcast
    // ──────────────────────────────────────────────

    public async Task BroadcastClockUpdatesAsync()
    {
        var activeGames = await db.LiveGames
            .Where(lg => lg.Status == LiveGameStatus.InProgress)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var game in activeGames)
        {
            var moveTokens = MoveDataParser.ParseMoves(game.MoveData);
            if (moveTokens.Count == 0) continue;

            var lastMove = moveTokens[^1];
            long elapsed = now - lastMove.EpochMs;
            bool isWhiteTurn = MoveDataParser.IsWhiteTurn(game.MoveData);

            long whiteClockMs = isWhiteTurn ? game.WhiteClockMs - elapsed : game.WhiteClockMs;
            long blackClockMs = !isWhiteTurn ? game.BlackClockMs - elapsed : game.BlackClockMs;

            await liveGameHub.Clients.Group($"game:{game.Id}").SendAsync("ClockUpdate", new
            {
                liveGameId = game.Id,
                whiteClockMs = Math.Max(0, whiteClockMs),
                blackClockMs = Math.Max(0, blackClockMs),
            });
        }
    }

    // ──────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────

    private async Task<LiveGame?> LoadGameAsync(Guid liveGameId)
    {
        return await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.WhitePlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.BlackPlayer)
            .FirstOrDefaultAsync(lg => lg.Id == liveGameId);
    }

    private static ChessBoard? ReplayBoard(string moveData)
    {
        try
        {
            var board = new ChessBoard { AutoEndgameRules = AutoEndgameRules.All };
            var moveTokens = MoveDataParser.ParseMoves(moveData);
            foreach (var token in moveTokens)
            {
                board.Move(token.San);
            }
            return board;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(bool Success, string? Error, object? Broadcast)> HandleTimeoutAsync(
        LiveGame liveGame, bool isWhiteTurn, long now)
    {
        int ply = MoveDataParser.CurrentPly(liveGame.MoveData) + 1;

        if (isWhiteTurn)
            liveGame.WhiteClockMs = 0;
        else
            liveGame.BlackClockMs = 0;

        liveGame.MoveData = MoveDataParser.AppendToken(liveGame.MoveData, ply, "timeout", 0, now);
        liveGame.Status = LiveGameStatus.Completed;
        liveGame.CompletedAt = DateTime.UtcNow;

        // The player whose turn it is lost on time
        liveGame.Match.Result = isWhiteTurn ? MatchResult.BlackWin : MatchResult.WhiteWin;

        await db.SaveChangesAsync();

        await BroadcastGameEndedAsync(liveGame, "timeout");

        return (true, null, null);
    }

    private async Task HandleBoardEndGameAsync(LiveGame liveGame, ChessBoard board, long now)
    {
        liveGame.Status = LiveGameStatus.Completed;
        liveGame.CompletedAt = DateTime.UtcNow;

        if (board.EndGame!.EndgameType == EndgameType.Checkmate)
        {
            liveGame.Match.Result = board.EndGame.WonSide == PieceColor.White
                ? MatchResult.WhiteWin
                : MatchResult.BlackWin;
        }
        else
        {
            // Stalemate, insufficient material, repetition, fifty-move rule → draw
            liveGame.Match.Result = MatchResult.Draw;
        }
    }

    private async Task BroadcastGameEndedAsync(LiveGame liveGame, string? reason = null)
    {
        var match = liveGame.Match;
        var payload = new
        {
            liveGameId = liveGame.Id,
            result = match.Result?.ToString(),
            reason = reason ?? liveGame.Status.ToString().ToLowerInvariant(),
            finalMoveData = liveGame.MoveData,
        };

        await liveGameHub.Clients.Group($"game:{liveGame.Id}").SendAsync("GameEnded", payload);

        // Also notify TournamentHub
        var slug = await GetTournamentSlugAsync(liveGame);
        if (slug is not null)
        {
            await tournamentHub.Clients.Group(slug).SendAsync("GameEnded", payload);
        }
    }

    private async Task<string?> GetTournamentSlugAsync(LiveGame liveGame)
    {
        var round = await db.Rounds
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.Id == liveGame.Match.RoundId);

        return round?.Tournament.Slug;
    }
}
