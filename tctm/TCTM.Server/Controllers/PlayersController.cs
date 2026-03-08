using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Mappings;
using TCTM.Server.Services;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}/players")]
public class PlayersController(TctmDbContext db, TournamentNotificationService notifier) : ControllerBase
{
    /// <summary>GET /api/tournaments/{slug}/players — List players.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        var players = tournament.Players
            .OrderBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName)
            .Select(p => p.ToDto())
            .ToList();

        return Ok(players);
    }

    /// <summary>DELETE /api/tournaments/{slug}/players/{id} — Remove a player (admin).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(string slug, Guid id, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        if (tournament.Status != TournamentStatus.Lobby)
            return BadRequest(new { error = "Cannot remove players after the tournament has started." });

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == id && p.TournamentId == tournament.Id);

        if (player is null)
            return NotFound();

        db.Players.Remove(player);
        await db.SaveChangesAsync();

        await notifier.PlayerRemoved(slug, id);

        return NoContent();
    }

    /// <summary>POST /api/tournaments/{slug}/players/{id}/reset-token — Reset a player's token (admin).</summary>
    [HttpPost("{id:guid}/reset-token")]
    public async Task<IActionResult> ResetToken(string slug, Guid id, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == id && p.TournamentId == tournament.Id);

        if (player is null)
            return NotFound();

        var newToken = GenerateToken();
        player.PlayerToken = TournamentsController.HashToken(newToken);
        await db.SaveChangesAsync();

        return Ok(new ResetTokenResponse(player.Id, player.DisplayName, newToken));
    }

    /// <summary>PUT /api/tournaments/{slug}/players/seed — Set seed order (admin).</summary>
    [HttpPut("seed")]
    public async Task<IActionResult> SetSeedOrder(string slug, [FromBody] SetSeedOrderRequest request, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        if (tournament.Status != TournamentStatus.Lobby)
            return BadRequest(new { error = "Cannot change seeding after the tournament has started." });

        var playerMap = tournament.Players.ToDictionary(p => p.Id);

        for (int i = 0; i < request.PlayerIds.Count; i++)
        {
            if (!playerMap.TryGetValue(request.PlayerIds[i], out var player))
                return BadRequest(new { error = $"Player {request.PlayerIds[i]} not found in this tournament." });

            player.Seed = i + 1;
        }

        await db.SaveChangesAsync();

        var playersDto = tournament.Players
            .OrderBy(p => p.Seed)
            .Select(p => p.ToDto())
            .ToList();

        await notifier.SeedOrderUpdated(slug, playersDto);

        return Ok(playersDto);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
