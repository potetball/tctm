namespace TCTM.Server.Services;

public static class MoveDataParser
{
    public record MoveToken(int Ply, string San, long ClockMs, long EpochMs);

    /// <summary>
    /// Special SAN values that represent control tokens rather than chess moves.
    /// </summary>
    public static readonly HashSet<string> ControlTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "resign", "timeout", "draw-offer", "draw-accept", "abort"
    };

    public static List<MoveToken> Parse(string moveData)
    {
        if (string.IsNullOrEmpty(moveData)) return [];

        return moveData.Split('|').Select(token =>
        {
            var parts = token.Split(':');
            return new MoveToken(
                int.Parse(parts[0]),
                parts[1],
                long.Parse(parts[2]),
                long.Parse(parts[3])
            );
        }).ToList();
    }

    public static string AppendToken(string moveData, int ply, string san, long clockMs, long epochMs)
    {
        var token = $"{ply}:{san}:{clockMs}:{epochMs}";
        return string.IsNullOrEmpty(moveData) ? token : $"{moveData}|{token}";
    }

    public static int CurrentPly(string moveData)
        => string.IsNullOrEmpty(moveData) ? 0 : Parse(moveData).Count(t => !IsControlToken(t));

    public static bool IsWhiteTurn(string moveData)
        => CurrentPly(moveData) % 2 == 0; // 0 moves played → White's turn (ply 1)

    public static bool IsControlToken(MoveToken token)
        => ControlTokens.Contains(token.San);

    /// <summary>
    /// Check if there is a pending draw offer (draw-offer is the last token and no draw-accept follows).
    /// </summary>
    public static bool HasPendingDrawOffer(string moveData)
    {
        if (string.IsNullOrEmpty(moveData)) return false;

        var tokens = Parse(moveData);
        if (tokens.Count == 0) return false;

        var last = tokens[^1];
        return last.San == "draw-offer";
    }

    /// <summary>
    /// Returns only the actual chess move tokens (excludes control tokens).
    /// </summary>
    public static List<MoveToken> ParseMoves(string moveData)
        => Parse(moveData).Where(t => !IsControlToken(t)).ToList();
}
