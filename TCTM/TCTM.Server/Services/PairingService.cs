using TCTM.Server.DataModel;

namespace TCTM.Server.Services;

/// <summary>
/// Generates match pairings for each tournament format.
/// </summary>
public static class PairingService
{
    /// <summary>
    /// Generate the first round of matches for a tournament that is just starting.
    /// Players should already be loaded on the tournament.
    /// Returns the created Round (with Matches populated but not yet saved to DB).
    /// </summary>
    public static Round GenerateFirstRound(Tournament tournament, List<Player> players)
    {
        var round = tournament.Format switch
        {
            TournamentFormat.RoundRobin => GenerateRoundRobinRound(tournament, players, roundNumber: 1),
            TournamentFormat.Swiss => GenerateSwissFirstRound(tournament, players),
            TournamentFormat.SingleElimination => GenerateSingleEliminationFirstRound(tournament, players),
            TournamentFormat.DoubleElimination => GenerateDoubleEliminationFirstRound(tournament, players),
            _ => throw new InvalidOperationException($"Unsupported format: {tournament.Format}")
        };

        if (tournament.PlayBothColors)
            AddColorSwappedMatches(round);

        return round;
    }

    /// <summary>
    /// Generate the next round for an in-progress tournament.
    /// </summary>
    public static Round GenerateNextRound(Tournament tournament, List<Player> players, List<Round> completedRounds)
    {
        var nextRoundNumber = completedRounds.Count + 1;

        var round = tournament.Format switch
        {
            TournamentFormat.RoundRobin => GenerateRoundRobinRound(tournament, players, nextRoundNumber),
            TournamentFormat.Swiss => GenerateSwissNextRound(tournament, players, completedRounds, nextRoundNumber),
            TournamentFormat.SingleElimination => GenerateEliminationNextRound(tournament, completedRounds, nextRoundNumber, bracket: null),
            TournamentFormat.DoubleElimination => GenerateDoubleEliminationNextRound(tournament, players, completedRounds, nextRoundNumber),
            _ => throw new InvalidOperationException($"Unsupported format: {tournament.Format}")
        };

        if (tournament.PlayBothColors)
            AddColorSwappedMatches(round);

        return round;
    }

    // ---------------------------------------------------------------
    //  ROUND ROBIN
    // ---------------------------------------------------------------

    /// <summary>
    /// Generates pairings for the given round number using the circle (polygon) method.
    /// Fix the first player; rotate the rest.
    /// </summary>
    private static Round GenerateRoundRobinRound(Tournament tournament, List<Player> players, int roundNumber)
    {
        // Order by seed (if set), then by display name for deterministic ordering
        var ordered = players
            .OrderBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // If odd number of players, add a null placeholder for byes
        var hasBye = ordered.Count % 2 != 0;
        var circle = ordered.Select(p => (Player?)p).ToList();
        if (hasBye)
            circle.Add(null); // bye placeholder

        int n = circle.Count;
        // Circle method: fix circle[0], rotate the rest by (roundNumber - 1) positions
        var rotated = new List<Player?>(n) { circle[0] };
        for (int i = 1; i < n; i++)
        {
            int idx = 1 + ((i - 1 + (roundNumber - 1)) % (n - 1));
            rotated.Add(circle[idx]);
        }

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            Status = RoundStatus.InProgress
        };

        // Pair first with last, second with second-to-last, etc.
        for (int i = 0; i < n / 2; i++)
        {
            var p1 = rotated[i];
            var p2 = rotated[n - 1 - i];

            // Alternate colors: even board → p1 is white, odd board → p2 is white
            var (white, black) = (i % 2 == 0) ? (p1, p2) : (p2, p1);

            var match = new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = white?.Id,
                BlackPlayerId = black?.Id,
                Result = HasBye(white, black) ? GetByeResult(white) : null,
                Disputed = false,
                Bracket = null
            };

            round.Matches.Add(match);
        }

        return round;
    }

    // ---------------------------------------------------------------
    //  SWISS
    // ---------------------------------------------------------------

    /// <summary>
    /// Swiss round 1: order by seed, pair top-half vs bottom-half.
    /// </summary>
    private static Round GenerateSwissFirstRound(Tournament tournament, List<Player> players)
    {
        var ordered = players
            .OrderBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = 1,
            Status = RoundStatus.InProgress
        };

        // If odd, the last player gets a bye
        int pairCount = ordered.Count / 2;
        bool hasBye = ordered.Count % 2 != 0;

        // Top-half vs bottom-half: player[0] vs player[pairCount], player[1] vs player[pairCount+1], ...
        for (int i = 0; i < pairCount; i++)
        {
            var white = ordered[i];
            var black = ordered[pairCount + i];

            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = white.Id,
                BlackPlayerId = black.Id,
                Result = null,
                Disputed = false,
                Bracket = null
            });
        }

        if (hasBye)
        {
            var byePlayer = ordered.Last();
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = byePlayer.Id,
                BlackPlayerId = null, // bye
                Result = MatchResult.WhiteWin, // auto-win for bye
                Disputed = false,
                Bracket = null
            });
        }

        return round;
    }

    /// <summary>
    /// Swiss subsequent rounds: within each score group, pair top-half vs bottom-half,
    /// avoiding repeat pairings (simplified Dutch system).
    /// </summary>
    private static Round GenerateSwissNextRound(Tournament tournament, List<Player> players, List<Round> completedRounds, int roundNumber)
    {
        // Calculate scores
        var scores = CalculateScores(players, completedRounds);
        var previousPairings = GetPreviousPairings(completedRounds);

        // Order by score (descending), then seed, then name
        var ordered = players
            .OrderByDescending(p => scores.GetValueOrDefault(p.Id, 0))
            .ThenBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Track who has had a bye
        var previousByePlayers = GetPreviousByePlayers(completedRounds);

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            Status = RoundStatus.InProgress
        };

        var paired = new HashSet<Guid>();

        // Group by score, then pair within each group
        var scoreGroups = ordered.GroupBy(p => scores.GetValueOrDefault(p.Id, 0))
            .OrderByDescending(g => g.Key)
            .ToList();

        var unpaired = new List<Player>();

        foreach (var group in scoreGroups)
        {
            var available = unpaired.Concat(group).Where(p => !paired.Contains(p.Id)).ToList();
            unpaired.Clear();

            int half = available.Count / 2;

            for (int i = 0; i < half; i++)
            {
                var white = available[i];
                var black = available[half + i];

                // Check for repeat pairing and try to swap within the group
                if (previousPairings.Contains(PairKey(white.Id, black.Id)))
                {
                    bool swapped = false;
                    for (int j = half + i + 1; j < available.Count; j++)
                    {
                        if (!previousPairings.Contains(PairKey(white.Id, available[j].Id)))
                        {
                            (available[half + i], available[j]) = (available[j], available[half + i]);
                            black = available[half + i];
                            swapped = true;
                            break;
                        }
                    }
                    // If no swap found, pair anyway (last resort)
                    if (!swapped) { /* proceed with repeat */ }
                }

                paired.Add(white.Id);
                paired.Add(black.Id);

                round.Matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    RoundId = round.Id,
                    WhitePlayerId = white.Id,
                    BlackPlayerId = black.Id,
                    Result = null,
                    Disputed = false,
                    Bracket = null
                });
            }

            // Any leftover from this score group carries down
            foreach (var p in available.Where(p => !paired.Contains(p.Id)))
                unpaired.Add(p);
        }

        // If there's one remaining player, assign a bye
        if (unpaired.Count == 1)
        {
            var byePlayer = unpaired[0];
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = byePlayer.Id,
                BlackPlayerId = null,
                Result = MatchResult.WhiteWin,
                Disputed = false,
                Bracket = null
            });
        }

        return round;
    }

    // ---------------------------------------------------------------
    //  SINGLE ELIMINATION
    // ---------------------------------------------------------------

    /// <summary>
    /// Single elimination round 1: seed into bracket, assign byes to fill to power of 2.
    /// Standard bracket seeding: 1v(n), 2v(n-1), etc.
    /// Highest seeds get byes.
    /// </summary>
    private static Round GenerateSingleEliminationFirstRound(Tournament tournament, List<Player> players)
    {
        var seeded = players
            .OrderBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int n = seeded.Count;
        int bracketSize = NextPowerOfTwo(n);
        int byes = bracketSize - n;

        // Create seed slots: fill with players, then nulls for byes
        var slots = new Player?[bracketSize];
        for (int i = 0; i < n; i++)
            slots[i] = seeded[i];
        // Remaining slots are null (byes)

        // Generate standard bracket matchups using the seeding formula
        var matchups = GenerateBracketMatchups(bracketSize);

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = 1,
            Status = RoundStatus.InProgress
        };

        foreach (var (seed1, seed2) in matchups)
        {
            var white = slots[seed1 - 1]; // seeds are 1-based
            var black = slots[seed2 - 1];

            var match = new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = white?.Id,
                BlackPlayerId = black?.Id,
                Result = HasBye(white, black) ? GetByeResult(white) : null,
                Disputed = false,
                Bracket = null
            };

            round.Matches.Add(match);
        }

        return round;
    }

    /// <summary>
    /// Generate the next elimination round based on winners of the previous round.
    /// </summary>
    private static Round GenerateEliminationNextRound(Tournament tournament, List<Round> completedRounds, int roundNumber, Bracket? bracket)
    {
        var lastRound = completedRounds
            .Where(r => bracket == null || r.Matches.All(m => m.Bracket == bracket || m.Bracket == null))
            .OrderByDescending(r => r.RoundNumber)
            .First();

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            Status = RoundStatus.InProgress
        };

        var winners = lastRound.Matches
            .Select(GetMatchWinnerId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        // Pair winners sequentially: match 1 winner vs match 2 winner, etc.
        for (int i = 0; i + 1 < winners.Count; i += 2)
        {
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = winners[i],
                BlackPlayerId = winners[i + 1],
                Result = null,
                Disputed = false,
                Bracket = bracket
            });
        }

        // Odd winner out → bye/auto-advance
        if (winners.Count % 2 != 0)
        {
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = winners.Last(),
                BlackPlayerId = null,
                Result = MatchResult.WhiteWin,
                Disputed = false,
                Bracket = bracket
            });
        }

        return round;
    }

    // ---------------------------------------------------------------
    //  DOUBLE ELIMINATION
    // ---------------------------------------------------------------

    /// <summary>
    /// Double elimination round 1: same as single elimination but matches are in Winners bracket.
    /// </summary>
    private static Round GenerateDoubleEliminationFirstRound(Tournament tournament, List<Player> players)
    {
        var seeded = players
            .OrderBy(p => p.Seed ?? int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int n = seeded.Count;
        int bracketSize = NextPowerOfTwo(n);

        var slots = new Player?[bracketSize];
        for (int i = 0; i < n; i++)
            slots[i] = seeded[i];

        var matchups = GenerateBracketMatchups(bracketSize);

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = 1,
            Status = RoundStatus.InProgress
        };

        foreach (var (seed1, seed2) in matchups)
        {
            var white = slots[seed1 - 1];
            var black = slots[seed2 - 1];

            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = white?.Id,
                BlackPlayerId = black?.Id,
                Result = HasBye(white, black) ? GetByeResult(white) : null,
                Disputed = false,
                Bracket = Bracket.Winners
            });
        }

        return round;
    }

    /// <summary>
    /// Subsequent rounds for double elimination (simplified).
    /// Advances winners in Winners bracket; losers drop to Losers bracket.
    /// </summary>
    private static Round GenerateDoubleEliminationNextRound(Tournament tournament, List<Player> players, List<Round> completedRounds, int roundNumber)
    {
        var lastRound = completedRounds.OrderByDescending(r => r.RoundNumber).First();

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            Status = RoundStatus.InProgress
        };

        // Collect all eliminated players (losers from this round's Winners bracket matches)
        var winnersMatches = lastRound.Matches.Where(m => m.Bracket == Bracket.Winners).ToList();
        var losersMatches = lastRound.Matches.Where(m => m.Bracket == Bracket.Losers).ToList();

        // Winners bracket: advance winners
        var winnersWinners = winnersMatches
            .Select(GetMatchWinnerId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        for (int i = 0; i + 1 < winnersWinners.Count; i += 2)
        {
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = winnersWinners[i],
                BlackPlayerId = winnersWinners[i + 1],
                Result = null,
                Disputed = false,
                Bracket = Bracket.Winners
            });
        }

        // New losers from winners bracket drop down
        var newLosers = winnersMatches
            .Select(GetMatchLoserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        // Losers bracket survivors
        var losersSurvivors = losersMatches
            .Select(GetMatchWinnerId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        // Combine losers bracket pool: survivors + new drop-downs
        var losersPool = losersSurvivors.Concat(newLosers).ToList();

        for (int i = 0; i + 1 < losersPool.Count; i += 2)
        {
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = losersPool[i],
                BlackPlayerId = losersPool[i + 1],
                Result = null,
                Disputed = false,
                Bracket = Bracket.Losers
            });
        }

        // If one leftover in losers pool, they get a bye/auto-advance
        if (losersPool.Count % 2 != 0)
        {
            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = losersPool.Last(),
                BlackPlayerId = null,
                Result = MatchResult.WhiteWin,
                Disputed = false,
                Bracket = Bracket.Losers
            });
        }

        return round;
    }

    // ---------------------------------------------------------------
    //  HELPERS
    // ---------------------------------------------------------------

    /// <summary>Standard bracket matchups for a given bracket size (power of 2).</summary>
    private static List<(int Seed1, int Seed2)> GenerateBracketMatchups(int bracketSize)
    {
        // Recursive bracket generation: for a bracket of size N,
        // seed 1 plays seed N, seed 2 plays seed N-1, etc.
        // But in a proper bracket, the matchups are arranged so that
        // if seeds win, 1 meets 2 in the final.
        // We use the standard seeding formula.
        var seeds = GenerateBracketSeeds(bracketSize);
        var matchups = new List<(int, int)>();
        for (int i = 0; i < seeds.Count; i += 2)
        {
            matchups.Add((seeds[i], seeds[i + 1]));
        }
        return matchups;
    }

    /// <summary>
    /// Generate standard bracket seed positions.
    /// For a bracket of 2: [1, 2]
    /// For a bracket of 4: [1, 4, 3, 2]  → matches are 1v4, 3v2
    /// For a bracket of 8: [1, 8, 5, 4, 3, 6, 7, 2] → matches are 1v8, 5v4, 3v6, 7v2
    /// </summary>
    private static List<int> GenerateBracketSeeds(int size)
    {
        if (size == 1) return [1];

        var half = GenerateBracketSeeds(size / 2);
        var result = new List<int>(size);

        foreach (var seed in half)
        {
            result.Add(seed);
            result.Add(size + 1 - seed);
        }

        return result;
    }

    private static int NextPowerOfTwo(int n)
    {
        int power = 1;
        while (power < n) power <<= 1;
        return power;
    }

    private static bool HasBye(Player? white, Player? black) => white is null || black is null;

    /// <summary>
    /// When one side is a bye (null), the present player wins automatically.
    /// </summary>
    private static MatchResult? GetByeResult(Player? white)
    {
        return white is not null ? MatchResult.WhiteWin : MatchResult.BlackWin;
    }

    private static Guid? GetMatchWinnerId(Match m)
    {
        return m.Result switch
        {
            MatchResult.WhiteWin => m.WhitePlayerId,
            MatchResult.BlackWin => m.BlackPlayerId,
            MatchResult.Draw => m.WhitePlayerId, // in elimination, draw shouldn't happen; default to white
            _ => null
        };
    }

    private static Guid? GetMatchLoserId(Match m)
    {
        return m.Result switch
        {
            MatchResult.WhiteWin => m.BlackPlayerId,
            MatchResult.BlackWin => m.WhitePlayerId,
            MatchResult.Draw => m.BlackPlayerId,
            _ => null
        };
    }

    private static Dictionary<Guid, double> CalculateScores(List<Player> players, List<Round> rounds)
    {
        var scores = players.ToDictionary(p => p.Id, _ => 0.0);

        foreach (var round in rounds)
        {
            foreach (var match in round.Matches)
            {
                switch (match.Result)
                {
                    case MatchResult.WhiteWin when match.WhitePlayerId.HasValue:
                        scores[match.WhitePlayerId.Value] += 1.0;
                        break;
                    case MatchResult.BlackWin when match.BlackPlayerId.HasValue:
                        scores[match.BlackPlayerId.Value] += 1.0;
                        break;
                    case MatchResult.Draw:
                        if (match.WhitePlayerId.HasValue) scores[match.WhitePlayerId.Value] += 0.5;
                        if (match.BlackPlayerId.HasValue) scores[match.BlackPlayerId.Value] += 0.5;
                        break;
                }
            }
        }

        return scores;
    }

    private static HashSet<(Guid, Guid)> GetPreviousPairings(List<Round> rounds)
    {
        var pairings = new HashSet<(Guid, Guid)>();
        foreach (var round in rounds)
        {
            foreach (var match in round.Matches)
            {
                if (match.WhitePlayerId.HasValue && match.BlackPlayerId.HasValue)
                {
                    pairings.Add(PairKey(match.WhitePlayerId.Value, match.BlackPlayerId.Value));
                }
            }
        }
        return pairings;
    }

    private static (Guid, Guid) PairKey(Guid a, Guid b)
    {
        return a.CompareTo(b) <= 0 ? (a, b) : (b, a);
    }

    private static HashSet<Guid> GetPreviousByePlayers(List<Round> rounds)
    {
        var byePlayers = new HashSet<Guid>();
        foreach (var round in rounds)
        {
            foreach (var match in round.Matches)
            {
                if (match.WhitePlayerId.HasValue && !match.BlackPlayerId.HasValue)
                    byePlayers.Add(match.WhitePlayerId.Value);
                if (!match.WhitePlayerId.HasValue && match.BlackPlayerId.HasValue)
                    byePlayers.Add(match.BlackPlayerId.Value);
            }
        }
        return byePlayers;
    }

    /// <summary>
    /// When PlayBothColors is enabled, duplicate every non-bye match in the round
    /// with white and black swapped so each pairing is played from both sides.
    /// Bye matches are not duplicated (a bye is a bye regardless of color).
    /// </summary>
    private static void AddColorSwappedMatches(Round round)
    {
        var originals = round.Matches.ToList();

        foreach (var match in originals)
        {
            // Skip byes — no need to swap a bye
            if (!match.WhitePlayerId.HasValue || !match.BlackPlayerId.HasValue)
                continue;

            round.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                WhitePlayerId = match.BlackPlayerId,   // swapped
                BlackPlayerId = match.WhitePlayerId,   // swapped
                Result = null,
                Disputed = false,
                Bracket = match.Bracket
            });
        }
    }
}
