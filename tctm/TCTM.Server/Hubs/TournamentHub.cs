using Microsoft.AspNetCore.SignalR;

namespace TCTM.Server.Hubs;

/// <summary>
/// SignalR hub for real-time tournament updates.
/// Clients join a group keyed by the tournament slug so they only
/// receive events for the tournament they are viewing.
/// </summary>
public class TournamentHub : Hub
{
    /// <summary>
    /// Called by clients to subscribe to updates for a specific tournament.
    /// </summary>
    /// <param name="slug">The tournament slug to join.</param>
    public async Task JoinTournament(string slug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, slug);
        await Clients.Caller.SendAsync("JoinedTournament", slug);
    }

    /// <summary>
    /// Called by clients to unsubscribe from a tournament's updates.
    /// </summary>
    /// <param name="slug">The tournament slug to leave.</param>
    public async Task LeaveTournament(string slug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, slug);
    }
}
