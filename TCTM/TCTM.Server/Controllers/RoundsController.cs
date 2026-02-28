using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Mappings;
using TCTM.Server.Services;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}/rounds")]
public class RoundsController(TctmDbContext db) : ControllerBase
{
    /// <summary>GET /api/tournaments/{slug}/rounds — List rounds with matches.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        var rounds = await db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .Include(r => r.Matches).ThenInclude(m => m.WhitePlayer)
            .Include(r => r.Matches).ThenInclude(m => m.BlackPlayer)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        return Ok(rounds.Select(r => r.ToDto()).ToList());
    }

    /// <summary>POST /api/tournaments/{slug}/rounds/next — Generate next round (admin).</summary>
    [HttpPost("next")]
    public async Task<IActionResult> GenerateNext(string slug, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Rounds)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        if (tournament.Status != TournamentStatus.InProgress)
            return BadRequest(new { error = "Tournament is not in progress." });

        // Check that the current round (if any) is completed
        var lastRound = tournament.Rounds.OrderByDescending(r => r.RoundNumber).FirstOrDefault();
        if (lastRound is not null && lastRound.Status != RoundStatus.Completed)
            return BadRequest(new { error = "The current round is not yet completed." });

        // Load players and full round data for pairing generation
        await db.Entry(tournament).Collection(t => t.Players).LoadAsync();
        var completedRounds = await db.Rounds
            .Where(r => r.TournamentId == tournament.Id && r.Status == RoundStatus.Completed)
            .Include(r => r.Matches)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        var players = tournament.Players.ToList();
        var round = PairingService.GenerateNextRound(tournament, players, completedRounds);

        db.Rounds.Add(round);
        foreach (var match in round.Matches)
            db.Matches.Add(match);
        await db.SaveChangesAsync();

        // Reload with matches/players for DTO
        var created = await db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.WhitePlayer)
            .Include(r => r.Matches).ThenInclude(m => m.BlackPlayer)
            .FirstAsync(r => r.Id == round.Id);

        return CreatedAtAction(nameof(List), new { slug }, created.ToDto());
    }
}
