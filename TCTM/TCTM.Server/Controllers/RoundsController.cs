using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
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

    /// <summary>POST /api/tournaments/{slug}/rounds/{roundNumber}/complete — Complete a round and recalculate standings (admin).</summary>
    [HttpPost("{roundNumber:int}/complete")]
    public async Task<IActionResult> CompleteRound(string slug, int roundNumber, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!TournamentsController.VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        var round = await db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.RoundNumber == roundNumber);

        if (round is null)
            return NotFound(new { error = $"Round {roundNumber} not found." });

        if (round.Status == RoundStatus.Completed)
            return BadRequest(new { error = "Round is already completed." });

        // Ensure all (non-bye) matches have results
        var pendingMatches = round.Matches
            .Where(m => m.WhitePlayerId is not null && m.BlackPlayerId is not null)
            .Where(m => m.Result is null)
            .ToList();

        if (pendingMatches.Count > 0)
            return BadRequest(new { error = $"{pendingMatches.Count} match(es) still have no result." });

        round.Status = RoundStatus.Completed;

        // Recalculate standings from all completed rounds
        var allCompletedRounds = await db.Rounds
            .Where(r => r.TournamentId == tournament.Id && (r.Status == RoundStatus.Completed || r.Id == round.Id))
            .Include(r => r.Matches)
            .ToListAsync();

        var standingsList = RecalculateStandings(tournament, allCompletedRounds);

        // Replace existing standings
        var existing = await db.Standings.Where(s => s.TournamentId == tournament.Id).ToListAsync();
        db.Standings.RemoveRange(existing);
        db.Standings.AddRange(standingsList);

        await db.SaveChangesAsync();

        // Return updated standings
        var result = await db.Standings
            .Where(s => s.TournamentId == tournament.Id)
            .Include(s => s.Player)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.SonnebornBerger)
            .ThenByDescending(s => s.Buchholz)
            .ToListAsync();

        return Ok(result.Select(s => s.ToDto()).ToList());
    }

    // --- Standings calculation helpers ---

    private static List<Standing> RecalculateStandings(Tournament tournament, List<Round> completedRounds)
    {
        var players = tournament.Players.ToList();
        var allMatches = completedRounds.SelectMany(r => r.Matches).ToList();

        // Points per player
        var stats = new Dictionary<Guid, (int Wins, int Draws, int Losses, double Points)>();
        foreach (var p in players)
            stats[p.Id] = (0, 0, 0, 0);

        foreach (var match in allMatches)
        {
            if (match.WhitePlayerId is null || match.BlackPlayerId is null)
                continue; // bye

            var wId = match.WhitePlayerId.Value;
            var bId = match.BlackPlayerId.Value;

            // Ensure entries exist
            if (!stats.ContainsKey(wId)) stats[wId] = (0, 0, 0, 0);
            if (!stats.ContainsKey(bId)) stats[bId] = (0, 0, 0, 0);

            switch (match.Result)
            {
                case MatchResult.WhiteWin:
                    stats[wId] = (stats[wId].Wins + 1, stats[wId].Draws, stats[wId].Losses, stats[wId].Points + 1);
                    stats[bId] = (stats[bId].Wins, stats[bId].Draws, stats[bId].Losses + 1, stats[bId].Points);
                    break;
                case MatchResult.BlackWin:
                    stats[wId] = (stats[wId].Wins, stats[wId].Draws, stats[wId].Losses + 1, stats[wId].Points);
                    stats[bId] = (stats[bId].Wins + 1, stats[bId].Draws, stats[bId].Losses, stats[bId].Points + 1);
                    break;
                case MatchResult.Draw:
                    stats[wId] = (stats[wId].Wins, stats[wId].Draws + 1, stats[wId].Losses, stats[wId].Points + 0.5);
                    stats[bId] = (stats[bId].Wins, stats[bId].Draws + 1, stats[bId].Losses, stats[bId].Points + 0.5);
                    break;
            }
        }

        // Buchholz: sum of opponents' points
        var opponentPoints = new Dictionary<Guid, List<double>>();
        foreach (var p in players)
            opponentPoints[p.Id] = [];

        foreach (var match in allMatches)
        {
            if (match.WhitePlayerId is null || match.BlackPlayerId is null) continue;

            var wId = match.WhitePlayerId.Value;
            var bId = match.BlackPlayerId.Value;

            if (stats.ContainsKey(bId))
                opponentPoints.GetValueOrDefault(wId)?.Add(stats[bId].Points);
            if (stats.ContainsKey(wId))
                opponentPoints.GetValueOrDefault(bId)?.Add(stats[wId].Points);
        }

        var standings = new List<Standing>();
        foreach (var p in players)
        {
            var s = stats.GetValueOrDefault(p.Id);
            var buchholz = opponentPoints.GetValueOrDefault(p.Id)?.Sum() ?? 0;

            // Sonneborn-Berger: sum of (points of beaten opponents) + 0.5 * (points of drawn opponents)
            double sonnebornBerger = 0;
            foreach (var match in allMatches)
            {
                if (match.WhitePlayerId is null || match.BlackPlayerId is null) continue;

                if (match.WhitePlayerId == p.Id)
                {
                    var oppId = match.BlackPlayerId.Value;
                    var oppPts = stats.GetValueOrDefault(oppId).Points;
                    if (match.Result == MatchResult.WhiteWin) sonnebornBerger += oppPts;
                    else if (match.Result == MatchResult.Draw) sonnebornBerger += oppPts * 0.5;
                }
                else if (match.BlackPlayerId == p.Id)
                {
                    var oppId = match.WhitePlayerId.Value;
                    var oppPts = stats.GetValueOrDefault(oppId).Points;
                    if (match.Result == MatchResult.BlackWin) sonnebornBerger += oppPts;
                    else if (match.Result == MatchResult.Draw) sonnebornBerger += oppPts * 0.5;
                }
            }

            standings.Add(new Standing
            {
                TournamentId = p.TournamentId,
                PlayerId = p.Id,
                Points = s.Points,
                Wins = s.Wins,
                Draws = s.Draws,
                Losses = s.Losses,
                Buchholz = buchholz,
                SonnebornBerger = sonnebornBerger
            });
        }

        return standings;
    }
}
