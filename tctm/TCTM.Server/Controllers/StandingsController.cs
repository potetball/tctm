using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Mappings;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}/standings")]
public class StandingsController(TctmDbContext db) : ControllerBase
{
    /// <summary>GET /api/tournaments/{slug}/standings — Get current standings.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string slug)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        var standings = await db.Standings
            .Where(s => s.TournamentId == tournament.Id)
            .Include(s => s.Player)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.SonnebornBerger)
            .ThenByDescending(s => s.Buchholz)
            .ToListAsync();

        return Ok(standings.Select(s => s.ToDto()).ToList());
    }
}
