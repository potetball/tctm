using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTM.Server.DataModel;
using TCTM.Server.Dto;
using TCTM.Server.Mappings;
using TCTM.Server.Services;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentsController(TctmDbContext db) : ControllerBase
{
    /// <summary>POST /api/tournaments — Create a new tournament.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTournamentRequest request)
    {
        var slug = GenerateSlug();
        var inviteCode = GenerateInviteCode();
        var adminToken = GenerateToken();

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name,
            InviteCode = inviteCode,
            AdminToken = HashToken(adminToken),
            Format = request.Format,
            TimeControlPreset = request.TimeControlPreset,
            TimeControlMinutes = request.TimeControlMinutes,
            PlayBothColors = request.PlayBothColors,
            Status = TournamentStatus.Lobby,
            CreatedAt = DateTime.UtcNow
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { slug }, new CreateTournamentResponse(
            slug,
            tournament.Name,
            inviteCode,
            adminToken
        ));
    }

    /// <summary>GET /api/tournaments/by-invite-code/{code} — Lookup tournament by invite code.</summary>
    [HttpGet("by-invite-code/{code}")]
    public async Task<IActionResult> GetByInviteCode(string code)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.InviteCode == code.ToUpperInvariant());

        if (tournament is null)
            return NotFound(new { error = "No tournament found with that invite code." });

        return Ok(tournament.ToDto());
    }

    /// <summary>GET /api/tournaments/{slug} — Get tournament details.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        return Ok(tournament.ToDto());
    }

    /// <summary>POST /api/tournaments/{slug}/join — Join a tournament.</summary>
    [HttpPost("{slug}/join")]
    public async Task<IActionResult> Join(string slug, [FromBody] JoinTournamentRequest request)
    {
        var tournament = await db.Tournaments
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (tournament.InviteCode != request.InviteCode)
            return BadRequest(new { error = "Invalid invite code." });

        if (tournament.Status != TournamentStatus.Lobby)
            return BadRequest(new { error = "Tournament has already started." });

        if (tournament.Players.Any(p => p.DisplayName == request.DisplayName))
            return Conflict(new { error = "Display name is already taken." });

        var playerToken = GenerateToken();

        var player = new Player
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            DisplayName = request.DisplayName,
            PlayerToken = HashToken(playerToken)
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return Ok(new JoinTournamentResponse(player.Id, playerToken));
    }

    /// <summary>POST /api/tournaments/reauthenticate — Verify a player token and return player info.</summary>
    [HttpPost("reauthenticate")]
    public async Task<IActionResult> Reauthenticate([FromBody] ReauthenticateRequest request)
    {
        var hashedToken = HashToken(request.PlayerToken);

        var player = await db.Players
            .Include(p => p.Tournament)
            .FirstOrDefaultAsync(p => p.PlayerToken == hashedToken);

        if (player is null)
            return Unauthorized(new { error = "Invalid player token." });

        return Ok(new ReauthenticateResponse(
            player.Tournament.Slug,
            player.Id,
            player.DisplayName
        ));
    }

    /// <summary>POST /api/tournaments/{slug}/start — Start the tournament (admin).</summary>
    [HttpPost("{slug}/start")]
    public async Task<IActionResult> Start(string slug, [FromHeader(Name = "X-Admin-Token")] string adminToken)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

        if (tournament is null)
            return NotFound();

        if (!VerifyToken(adminToken, tournament.AdminToken))
            return Unauthorized(new { error = "Invalid admin token." });

        if (tournament.Status != TournamentStatus.Lobby)
            return BadRequest(new { error = "Tournament is not in lobby." });

        // Load players for pairing
        await db.Entry(tournament).Collection(t => t.Players).LoadAsync();

        if (tournament.Players.Count < 2)
            return BadRequest(new { error = "Need at least 2 players to start." });

        tournament.Status = TournamentStatus.InProgress;

        // Generate first round pairings
        var players = tournament.Players.ToList();
        var firstRound = PairingService.GenerateFirstRound(tournament, players);

        db.Rounds.Add(firstRound);
        foreach (var match in firstRound.Matches)
            db.Matches.Add(match);

        await db.SaveChangesAsync();

        return Ok(tournament.ToDto());
    }

    // --- Helpers ---

    private static string GenerateSlug()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        return $"tctm-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return string.Create(6, chars, static (span, chars) =>
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            for (int i = 0; i < span.Length; i++)
                span[i] = chars[bytes[i] % chars.Length];
        });
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    internal static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    internal static bool VerifyToken(string token, string hashedToken)
    {
        return HashToken(token) == hashedToken;
    }
}
