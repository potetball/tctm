using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.Controllers;
using TCTM.Server.DataModel;
using TCTM.Server.Services;

namespace TCTM.Server.Hubs;

/// <summary>
/// Dedicated SignalR hub for live chess game real-time communication.
/// Separate from <see cref="TournamentHub"/> to isolate game-level traffic
/// (moves, clocks, draw offers) from tournament-level events.
/// </summary>
public class LiveGameHub(TctmDbContext db, LiveGameService gameService, GamePresenceTracker presence, ILogger<LiveGameHub> logger) : Hub
{
    /// <summary>Subscribe to a live game's move feed.</summary>
    public async Task<object?> JoinGame(Guid liveGameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game:{liveGameId}");

        // If caller is a participant, track presence and broadcast PlayerJoinedGame.
        // ResolvePlayerIdAsync handles both player-token and admin-with-playerId flows.
        var playerId = await ResolvePlayerIdAsync(liveGameId);

        if (playerId is not null)
        {
            var liveGame = await db.LiveGames
                .Include(lg => lg.Match)
                .FirstOrDefaultAsync(lg => lg.Id == liveGameId);

            if (liveGame is not null)
            {
                string? color = null;
                if (liveGame.Match.WhitePlayerId == playerId) color = "white";
                else if (liveGame.Match.BlackPlayerId == playerId) color = "black";

                if (color is not null)
                {
                    var presentPlayerIds = presence.PlayerJoined(Context.ConnectionId, liveGameId, playerId.Value);

                    await Clients.Group($"game:{liveGameId}").SendAsync("PlayerJoinedGame", new
                    {
                        liveGameId,
                        playerId,
                        color,
                    });

                    // Return the list of currently present player IDs to the caller
                    return new { presentPlayerIds };
                }
            }
        }

        return null;
    }

    /// <summary>Unsubscribe from a live game.</summary>
    public async Task LeaveGame(Guid liveGameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game:{liveGameId}");

        // If caller is a participant, track presence and broadcast PlayerLeftGame
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        if (playerId is not null)
        {
            var liveGame = await db.LiveGames
                .Include(lg => lg.Match)
                .FirstOrDefaultAsync(lg => lg.Id == liveGameId);

            if (liveGame is not null)
            {
                string? color = null;
                if (liveGame.Match.WhitePlayerId == playerId) color = "white";
                else if (liveGame.Match.BlackPlayerId == playerId) color = "black";

                if (color is not null)
                {
                    presence.PlayerLeft(Context.ConnectionId, liveGameId, playerId.Value);

                    await Clients.Group($"game:{liveGameId}").SendAsync("PlayerLeftGame", new
                    {
                        liveGameId,
                        playerId,
                        color,
                    });
                }
            }
        }
    }

    /// <summary>Clean up presence tracking when a connection is lost.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var entry = presence.ConnectionDisconnected(Context.ConnectionId);
        if (entry is not null)
        {
            var (gameId, pid) = entry.Value;
            await Clients.Group($"game:{gameId}").SendAsync("PlayerLeftGame", new
            {
                liveGameId = gameId,
                playerId = pid,
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Player submits a move. Requires player token in query string.</summary>
    public async Task SubmitMove(Guid liveGameId, string san, long clockMs)
    {
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        if (playerId is null)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Authentication required." });
            return;
        }

        var (success, error, _) = await gameService.SubmitMoveAsync(liveGameId, san, clockMs, playerId.Value);

        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    /// <summary>Player resigns.</summary>
    public async Task Resign(Guid liveGameId)
    {
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        if (playerId is null)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Authentication required." });
            return;
        }

        var (success, error) = await gameService.ResignAsync(liveGameId, playerId.Value);
        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    /// <summary>Offer a draw (must be the player's turn).</summary>
    public async Task OfferDraw(Guid liveGameId)
    {
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        if (playerId is null)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Authentication required." });
            return;
        }

        var (success, error) = await gameService.OfferDrawAsync(liveGameId, playerId.Value);
        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    /// <summary>Accept a pending draw offer.</summary>
    public async Task AcceptDraw(Guid liveGameId)
    {
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        if (playerId is null)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Authentication required." });
            return;
        }

        var (success, error) = await gameService.AcceptDrawAsync(liveGameId, playerId.Value);
        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    /// <summary>Abort the game (admin only).</summary>
    public async Task AbortGame(Guid liveGameId)
    {
        var isAdmin = await ResolveIsAdminAsync(liveGameId);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Admin authentication required." });
            return;
        }

        var (success, error) = await gameService.AbortGameAsync(liveGameId);
        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    /// <summary>Start the game. Per chess convention, Black starts White's clock.</summary>
    public async Task StartGame(Guid liveGameId)
    {
        var playerId = await ResolvePlayerIdAsync(liveGameId);
        var isAdmin = await ResolveIsAdminAsync(liveGameId);

        if (playerId is null && !isAdmin)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = "Authentication required." });
            return;
        }

        var (success, error) = await gameService.StartGameAsync(liveGameId, playerId ?? Guid.Empty, isAdmin);
        if (!success)
        {
            await Clients.Caller.SendAsync("MoveRejected", new { liveGameId, reason = error });
        }
    }

    // ──────────────────────────────────────────────
    //  Auth helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resolves the player ID from the connection query string.
    /// <para>
    /// Strategy (tried in order):
    /// <list type="number">
    ///   <item><description>
    ///     <b>Player token</b> — hash the <c>token</c> query param and look up a player
    ///     row whose <c>PlayerToken</c> matches.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Admin + explicit playerId</b> — if <c>token</c> is a valid admin token for
    ///     the game's tournament, and a <c>playerId</c> query param is provided, verify
    ///     that the claimed player belongs to the same tournament and is a participant in
    ///     the match. This lets a tournament organiser who is also a player connect with
    ///     their admin token and still be tracked for presence and move submission.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    private async Task<Guid?> ResolvePlayerIdAsync(Guid? liveGameId = null)
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token)) return null;

        // 1. Try player token lookup
        var hashedToken = TournamentsController.HashToken(token);
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayerToken == hashedToken);
        if (player is not null) return player.Id;

        // 2. Try admin + explicit playerId
        if (liveGameId is null) return null;

        var playerIdStr = httpContext?.Request.Query["playerId"].FirstOrDefault();
        if (string.IsNullOrEmpty(playerIdStr) || !Guid.TryParse(playerIdStr, out var claimedPlayerId))
            return null;

        var liveGame = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.Round)
                    .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(lg => lg.Id == liveGameId);

        if (liveGame is null) return null;

        // Verify admin token
        if (!TournamentsController.VerifyToken(token, liveGame.Match.Round.Tournament.AdminToken))
            return null;

        // Verify the claimed player is a participant in the match
        if (liveGame.Match.WhitePlayerId != claimedPlayerId && liveGame.Match.BlackPlayerId != claimedPlayerId)
            return null;

        // Verify the claimed player belongs to this tournament
        var claimedPlayer = await db.Players.FirstOrDefaultAsync(p =>
            p.Id == claimedPlayerId && p.TournamentId == liveGame.Match.Round.TournamentId);
        if (claimedPlayer is null) return null;

        return claimedPlayerId;
    }

    /// <summary>
    /// Checks whether the caller has a valid admin token for the tournament
    /// that contains the given live game.
    /// </summary>
    private async Task<bool> ResolveIsAdminAsync(Guid liveGameId)
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token)) return false;

        var liveGame = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.Round)
                    .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(lg => lg.Id == liveGameId);

        if (liveGame is null) return false;

        return TournamentsController.VerifyToken(token, liveGame.Match.Round.Tournament.AdminToken);
    }
}
