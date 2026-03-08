using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Mappings;
using TCTM.Server.Services;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}")]
public class LiveGamesController(TctmDbContext db, LiveGameService gameService) : ControllerBase
{
    /// <summary>
    /// GET /api/tournaments/{slug}/matches/{matchId}/live
    /// Get or auto-create a LiveGame for a match.
    /// If a "token" query param is provided and the caller is a participant, auto-creates if needed.
    /// </summary>
    [HttpGet("matches/{matchId:guid}/live")]
    public async Task<IActionResult> GetOrCreateLiveGame(string slug, Guid matchId, [FromQuery] string? token)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tournament is null) return NotFound();

        // Verify the match belongs to this tournament
        var match = await db.Matches
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.Round.TournamentId == tournament.Id);

        if (match is null) return NotFound();

        // Resolve caller player id if token provided
        Guid? callerPlayerId = null;
        if (!string.IsNullOrEmpty(token))
        {
            var hashedToken = TournamentsController.HashToken(token);
            var player = await db.Players.FirstOrDefaultAsync(p =>
                p.TournamentId == tournament.Id && p.PlayerToken == hashedToken);
            callerPlayerId = player?.Id;
        }

        var liveGame = await gameService.GetOrCreateAsync(matchId, slug, callerPlayerId);

        if (liveGame is null)
        {
            // No existing game and caller is not a participant — return 404
            return NotFound(new { error = "No live game exists for this match." });
        }

        return Ok(liveGame.ToDto());
    }

    /// <summary>
    /// GET /api/tournaments/{slug}/live-games
    /// List all live games in a tournament (any status).
    /// </summary>
    [HttpGet("live-games")]
    public async Task<IActionResult> ListLiveGames(string slug)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tournament is null) return NotFound();

        var liveGames = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.WhitePlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.BlackPlayer)
            .Include(lg => lg.Match)
                .ThenInclude(m => m.Round)
            .Where(lg => lg.Match.Round.TournamentId == tournament.Id)
            .ToListAsync();

        return Ok(liveGames.Select(lg => lg.ToSummaryDto()));
    }

    /// <summary>
    /// POST /api/tournaments/{slug}/matches/{matchId}/live/abort
    /// Abort a live game (admin only).
    /// </summary>
    [HttpPost("matches/{matchId:guid}/live/abort")]
    public async Task<IActionResult> AbortLiveGame(string slug, Guid matchId, [FromBody] AbortLiveGameRequest request)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tournament is null) return NotFound();

        if (!TournamentsController.VerifyToken(request.AdminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        var liveGame = await db.LiveGames
            .Include(lg => lg.Match)
                .ThenInclude(m => m.Round)
            .FirstOrDefaultAsync(lg => lg.MatchId == matchId && lg.Match.Round.TournamentId == tournament.Id);

        if (liveGame is null) return NotFound();

        var (success, error) = await gameService.AbortGameAsync(liveGame.Id);

        if (!success) return BadRequest(new { error });

        return Ok(new { message = "Game aborted." });
    }
}
