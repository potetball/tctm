using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Mappings;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}/players")]
public class PlayersController(TctmDbContext db) : ControllerBase
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

        return NoContent();
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

        return Ok(tournament.Players
            .OrderBy(p => p.Seed)
            .Select(p => p.ToDto())
            .ToList());
    }
}
