using Microsoft.EntityFrameworkCore;

namespace TCTM.Server.DataModel;

public class TctmDbContext(DbContextOptions<TctmDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Standing> Standings => Set<Standing>();
    public DbSet<LiveGame> LiveGames => Set<LiveGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tournament
        modelBuilder.Entity<Tournament>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.InviteCode).IsUnique();
            e.Property(t => t.Format).HasConversion<string>();
            e.Property(t => t.TimeControlPreset).HasConversion<string>();
            e.Property(t => t.Status).HasConversion<string>();
        });

        // Player
        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.TournamentId, p.DisplayName }).IsUnique();
            e.HasOne(p => p.Tournament)
             .WithMany(t => t.Players)
             .HasForeignKey(p => p.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Round
        modelBuilder.Entity<Round>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.TournamentId, r.RoundNumber }).IsUnique();
            e.Property(r => r.Status).HasConversion<string>();
            e.HasOne(r => r.Tournament)
             .WithMany(t => t.Rounds)
             .HasForeignKey(r => r.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Match
        modelBuilder.Entity<Match>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Result).HasConversion<string>();
            e.Property(m => m.Bracket).HasConversion<string>();
            e.HasOne(m => m.Round)
             .WithMany(r => r.Matches)
             .HasForeignKey(m => m.RoundId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.WhitePlayer)
             .WithMany(p => p.WhiteMatches)
             .HasForeignKey(m => m.WhitePlayerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.BlackPlayer)
             .WithMany(p => p.BlackMatches)
             .HasForeignKey(m => m.BlackPlayerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Standing (composite key)
        modelBuilder.Entity<Standing>(e =>
        {
            e.HasKey(s => new { s.TournamentId, s.PlayerId });
            e.HasOne(s => s.Tournament)
             .WithMany(t => t.Standings)
             .HasForeignKey(s => s.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Player)
             .WithMany()
             .HasForeignKey(s => s.PlayerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // LiveGame
        modelBuilder.Entity<LiveGame>(e =>
        {
            e.HasKey(lg => lg.Id);
            e.HasIndex(lg => lg.MatchId).IsUnique();
            e.Property(lg => lg.Status).HasConversion<string>();
            e.HasOne(lg => lg.Match)
             .WithOne(m => m.LiveGame)
             .HasForeignKey<LiveGame>(lg => lg.MatchId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
