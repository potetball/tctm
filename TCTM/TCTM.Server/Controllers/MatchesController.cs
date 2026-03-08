using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Mappings;
using TCTM.Server.Services;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments/{slug}/matches")]
public class MatchesController(TctmDbContext db, TournamentNotificationService notifier) : ControllerBase
{
    /// <summary>POST /api/tournaments/{slug}/matches/{id}/result — Report a match result (player or admin).</summary>
    [HttpPost("{id:guid}/result")]
    public async Task<IActionResult> ReportResult(string slug, Guid id, [FromBody] ReportResultRequest request)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        var match = await db.Matches
            .Include(m => m.WhitePlayer)
            .Include(m => m.BlackPlayer)
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == id && m.Round.TournamentId == tournament.Id);

        if (match is null)
            return NotFound();

        if (match.Round.Status != RoundStatus.InProgress)
            return BadRequest(new { error = "This round is not in progress." });

        // Identify reporter: admin or one of the two players
        bool isAdmin = TournamentsController.VerifyToken(request.Token, tournament.AdminToken);
        Guid? reportingPlayerId = null;

        if (!isAdmin)
        {
            var hashedToken = TournamentsController.HashToken(request.Token);

            // Find the player by their hashed token within this tournament
            var player = await db.Players.FirstOrDefaultAsync(p =>
                p.TournamentId == tournament.Id && p.PlayerToken == hashedToken);

            if (player is null)
                return Unauthorized(new { error = "Invalid token." });

            if (player.Id != match.WhitePlayerId && player.Id != match.BlackPlayerId)
                return Forbid();

            reportingPlayerId = player.Id;
        }

        // If there's already a result and the new report conflicts, flag as disputed
        if (match.Result is not null && match.Result != request.Result && !isAdmin)
        {
            match.Disputed = true;
            await db.SaveChangesAsync();
            return Conflict(new { error = "Result conflicts with previous report. Flagged for organiser review." });
        }

        match.Result = request.Result;
        match.ReportedBy = reportingPlayerId;

        // Admin reports clear disputes
        if (isAdmin)
            match.Disputed = false;

        await db.SaveChangesAsync();

        await notifier.MatchUpdated(slug, match.ToDto());

        return Ok(match.ToDto());
    }

    /// <summary>PUT /api/tournaments/{slug}/matches/{id}/result — Override a match result (admin only).</summary>
    [HttpPut("{id:guid}/result")]
    public async Task<IActionResult> OverrideResult(string slug, Guid id, [FromBody] OverrideResultRequest request)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(request.AdminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        var match = await db.Matches
            .Include(m => m.WhitePlayer)
            .Include(m => m.BlackPlayer)
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == id && m.Round.TournamentId == tournament.Id);

        if (match is null)
            return NotFound();

        match.Result = request.Result;
        match.ReportedBy = null; // organiser override
        match.Disputed = false;

        await db.SaveChangesAsync();

        await notifier.MatchUpdated(slug, match.ToDto());

        return Ok(match.ToDto());
    }
}
