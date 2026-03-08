using Microsoft.AspNetCore.SignalR;
using TCTM.Server.Hubs;

namespace TCTM.Server.Services;

/// <summary>
/// Thin wrapper around <see cref="IHubContext{TournamentHub}"/> that
/// broadcasts tournament events to the correct SignalR group.
/// </summary>
public class TournamentNotificationService(IHubContext<TournamentHub> hubContext)
{
    /// <summary>Notify that a player joined the lobby.</summary>
    public Task PlayerJoined(string slug, object player)
        => hubContext.Clients.Group(slug).SendAsync("PlayerJoined", player);

    /// <summary>Notify that a player was removed from the lobby.</summary>
    public Task PlayerRemoved(string slug, Guid playerId)
        => hubContext.Clients.Group(slug).SendAsync("PlayerRemoved", playerId);

    /// <summary>Notify that the tournament has started.</summary>
    public Task TournamentStarted(string slug, object tournament)
        => hubContext.Clients.Group(slug).SendAsync("TournamentStarted", tournament);

    /// <summary>Notify that a new round was generated.</summary>
    public Task RoundCreated(string slug, object round)
        => hubContext.Clients.Group(slug).SendAsync("RoundCreated", round);

    /// <summary>Notify that a round was completed and standings updated.</summary>
    public Task RoundCompleted(string slug, object round, object standings)
        => hubContext.Clients.Group(slug).SendAsync("RoundCompleted", round, standings);

    /// <summary>Notify that a match result was reported or updated.</summary>
    public Task MatchUpdated(string slug, object match)
        => hubContext.Clients.Group(slug).SendAsync("MatchUpdated", match);

    /// <summary>Notify that a player's seed order changed.</summary>
    public Task SeedOrderUpdated(string slug, object players)
        => hubContext.Clients.Group(slug).SendAsync("SeedOrderUpdated", players);
}
