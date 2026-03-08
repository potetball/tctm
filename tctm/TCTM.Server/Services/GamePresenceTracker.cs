using System.Collections.Concurrent;

namespace TCTM.Server.Services;

/// <summary>
/// In-memory tracker for which players (by player ID) are present in each live game room.
/// Also maps each SignalR connection to the game+player it belongs to, so we can clean up
/// on disconnect.
/// </summary>
public class GamePresenceTracker
{
    /// <summary>connectionId → (gameId, playerId)</summary>
    private readonly ConcurrentDictionary<string, (Guid GameId, Guid PlayerId)> _connections = new();

    /// <summary>gameId → set of playerIds currently present</summary>
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _gamePresence = new();

    private readonly object _lock = new();

    /// <summary>
    /// Record that a player joined a game room. Returns the current set of present player IDs
    /// (including the one just added).
    /// </summary>
    public List<Guid> PlayerJoined(string connectionId, Guid gameId, Guid playerId)
    {
        _connections[connectionId] = (gameId, playerId);

        lock (_lock)
        {
            if (!_gamePresence.TryGetValue(gameId, out var players))
            {
                players = new HashSet<Guid>();
                _gamePresence[gameId] = players;
            }
            players.Add(playerId);
            return players.ToList();
        }
    }

    /// <summary>
    /// Record that a player left a game room.
    /// </summary>
    public void PlayerLeft(string connectionId, Guid gameId, Guid playerId)
    {
        _connections.TryRemove(connectionId, out _);

        lock (_lock)
        {
            if (_gamePresence.TryGetValue(gameId, out var players))
            {
                players.Remove(playerId);
                if (players.Count == 0)
                    _gamePresence.TryRemove(gameId, out _);
            }
        }
    }

    /// <summary>
    /// Handle a disconnected connection. Returns the (gameId, playerId) if the connection
    /// was associated with a game player, or null otherwise.
    /// </summary>
    public (Guid GameId, Guid PlayerId)? ConnectionDisconnected(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var entry))
            return null;

        lock (_lock)
        {
            if (_gamePresence.TryGetValue(entry.GameId, out var players))
            {
                players.Remove(entry.PlayerId);
                if (players.Count == 0)
                    _gamePresence.TryRemove(entry.GameId, out _);
            }
        }

        return entry;
    }

    /// <summary>
    /// Get all player IDs currently present in a game room.
    /// </summary>
    public List<Guid> GetPresentPlayers(Guid gameId)
    {
        lock (_lock)
        {
            if (_gamePresence.TryGetValue(gameId, out var players))
                return players.ToList();
            return new List<Guid>();
        }
    }
}
