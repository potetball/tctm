# Live Chess Game — Specification

## 1. Overview

This specification extends TCTM with **live game tracking** — the ability to record and broadcast chess moves in real time via SignalR. The design is deliberately minimal: a **single new table** (`LiveGame`) stores the entire game state as a compact **encoded move string**. Timecodes, moves, and clock information are all packed into this one field, keeping the data model lean and the SignalR payload small.

> **Philosophy**: A chess game is a sequence of (move, timestamp) pairs. Rather than normalising this into rows-per-move, we encode the entire game as a single string that grows with each half-move. This makes reads trivial (one row = one game), writes append-only, and SignalR diffs tiny.

---

## 2. Encoded Move String Format

The `MoveData` column stores the full game as a **pipe-delimited** sequence of **move tokens**. Each token encodes one half-move (ply) together with its clock and wall-clock timestamp.

### 2.1 Token Structure

```
<ply>:<san>:<clockMs>:<epochMs>
```

| Field      | Type   | Description |
|------------|--------|-------------|
| `ply`      | int    | 1-based half-move number (1 = White's first move, 2 = Black's first move, etc.) |
| `san`      | string | Standard Algebraic Notation of the move (e.g. `e4`, `Nf3`, `O-O`, `Qxd7+`, `e8=Q`) |
| `clockMs`  | long   | Remaining clock time in **milliseconds** for the player who just moved |
| `epochMs`  | long   | Unix epoch timestamp in **milliseconds** when the move was recorded server-side |

### 2.2 Full String Example

A game opening might be encoded as:

```
1:e4:300000:1741334400000|2:e5:300000:1741334412000|3:Nf3:298500:1741334418000|4:Nc6:297200:1741334425000
```

This reads as:

| Ply | Move | Clock remaining | Wall-clock time |
|-----|------|----------------|-----------------|
| 1   | e4   | 5:00.000       | 2026-03-07T12:00:00.000Z |
| 2   | e5   | 5:00.000       | 2026-03-07T12:00:12.000Z |
| 3   | Nf3  | 4:58.500       | 2026-03-07T12:00:18.000Z |
| 4   | Nc6  | 4:57.200       | 2026-03-07T12:00:25.000Z |

### 2.3 Design Rationale

| Concern | Approach |
|---------|----------|
| **Compact storage** | A typical 40-move game ≈ 80 tokens × ~30 chars = ~2.4 KB. Well within SQLite's comfort zone. |
| **Append-only writes** | Each new move appends `\|<token>` to the string — no row inserts, no locking. |
| **Tiny SignalR payloads** | On each move the hub broadcasts only the **latest token** (≈ 30 bytes), not the full string. Clients reconstruct from their local state. |
| **Easy reconstruction** | Any client joining mid-game fetches the full string once, parses it, and replays to the current position. |
| **Clock fidelity** | Millisecond-precision clock values let the UI render a ticking clock without server polling. |
| **Auditability** | The epoch timestamp provides an immutable server-side record of when each move was received. |

### 2.4 Special Tokens

Beyond regular moves, the following **control tokens** can appear:

| Token pattern | Meaning |
|---------------|---------|
| `<ply>:resign:<clockMs>:<epochMs>` | The player whose turn it was resigned |
| `<ply>:timeout:<clockMs>:<epochMs>` | The player whose turn it was lost on time (clockMs will be 0) |
| `<ply>:draw-offer:<clockMs>:<epochMs>` | A draw was offered (informational, game continues) |
| `<ply>:draw-accept:<clockMs>:<epochMs>` | Draw offer accepted — game over |
| `<ply>:abort:<clockMs>:<epochMs>` | Game aborted (e.g. by organiser) |

These always appear as the **last token** in the string (except `draw-offer`, which may be followed by more moves or `draw-accept`).

**Draw offer lifecycle**: A draw can only be offered when it is the offering player's turn. The offer remains pending until the opponent either accepts (`draw-accept`) or **makes a move** — making a move implicitly declines and expires the draw offer. There is no explicit "decline" token; the offer simply lapses.

---

## 3. Data Model

### 3.1 LiveGame Table

A single table linked to an existing `Match`:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | GUID | PK |
| `MatchId` | GUID | FK → Match (unique — one live game per match) |
| `WhiteClockMs` | long | White's current remaining clock time in ms |
| `BlackClockMs` | long | Black's current remaining clock time in ms |
| `InitialClockMs` | long | Starting clock time in ms (derived from tournament TimeControlMinutes) |
| `MoveData` | string | The encoded move string (see §2). Empty string at game start. |
| `Status` | enum | `NotStarted`, `InProgress`, `Completed`, `Aborted` |
| `StartedAt` | DateTime? | When the first move was played |
| `CompletedAt` | DateTime? | When the game ended |

### 3.2 LiveGameStatus Enum

```csharp
public enum LiveGameStatus
{
    NotStarted,
    InProgress,
    Completed,
    Aborted
}
```

### 3.3 Entity Configuration

```csharp
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
```

### 3.4 C# Entity

```csharp
public class LiveGame
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public long WhiteClockMs { get; set; }
    public long BlackClockMs { get; set; }
    public long InitialClockMs { get; set; }
    public string MoveData { get; set; } = string.Empty;
    public LiveGameStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Match Match { get; set; } = null!;
}
```

---

## 4. SignalR Hub: `LiveGameHub`

A **dedicated** `LiveGameHub` (mapped to `/liveGameHub`) handles all game-level real-time communication. This is separate from the existing `TournamentHub` to keep concerns isolated:

| Reason | Detail |
|--------|--------|
| **Single Responsibility** | `TournamentHub` handles tournament-level events (bracket updates, round progression). Live game concerns (moves, clocks, draw offers) are a fundamentally different domain. |
| **Connection lifecycle** | Spectators watching a game connect only to `LiveGameHub` — they don't need `TournamentHub`. |
| **Traffic isolation** | Game move traffic is high-frequency (clock updates every 5s, rapid moves). Isolating it prevents tournament-level subscribers from receiving irrelevant messages. |
| **Auth granularity** | The two hubs can apply distinct authorization policies. |
| **Testability** | A focused hub with only game-related methods is easier to unit test. |

### 4.1 Cross-Hub Communication

When a game ends, the `LiveGameHub` (or its backing service) needs to notify tournament-level subscribers. This is done by injecting `IHubContext<TournamentHub>` into the live game service layer:

```csharp
// In LiveGameService or similar
public class LiveGameService
{
    private readonly IHubContext<TournamentHub> _tournamentHub;

    public LiveGameService(IHubContext<TournamentHub> tournamentHub) 
        => _tournamentHub = tournamentHub;

    // After a game ends, notify the tournament group
    public async Task NotifyTournamentOfGameEnd(string slug, object payload)
        => await _tournamentHub.Clients.Group($"tournament:{slug}")
            .SendAsync("GameEnded", payload);
}
```

### 4.2 Hub Groups

| Group key pattern | Hub | Scope |
|-------------------|-----|-------|
| `tournament:{slug}` | `TournamentHub` | All events for a tournament (existing) |
| `game:{liveGameId}` | `LiveGameHub` | Move-by-move updates for a specific live game |

### 4.3 Hub Methods (Client → Server)

| Method | Parameters | Description | Auth |
|--------|------------|-------------|------|
| `JoinGame` | `liveGameId` | Subscribe to a game's move feed | None (spectators welcome) |
| `LeaveGame` | `liveGameId` | Unsubscribe from a game | None |
| `SubmitMove` | `liveGameId`, `san`, `clockMs` | Player submits a move | Player token (must be the player whose turn it is) |
| `Resign` | `liveGameId` | Player resigns | Player token |
| `OfferDraw` | `liveGameId` | Offer a draw (must be the player's turn) | Player token (must be the player whose turn it is) |
| `AcceptDraw` | `liveGameId` | Accept a pending draw offer | Player token |
| `AbortGame` | `liveGameId` | Abort the game | Admin token |
| `StartGame` | `liveGameId` | Start the clock (White to move). Per chess convention, Black starts White's clock. | Admin token or **Black player** token |

### 4.4 Hub Events (Server → Client)

| Event | Payload | Sent to |
|-------|---------|---------|
| `GameStarted` | `{ liveGameId, whiteClockMs, blackClockMs }` | `game:{id}` group (`LiveGameHub`) |
| `MovePlayed` | `{ liveGameId, token, fen }` | `game:{id}` group (`LiveGameHub`) |
| `DrawOffered` | `{ liveGameId, offeredBy }` | `game:{id}` group (`LiveGameHub`) |
| `GameEnded` | `{ liveGameId, result, reason, finalMoveData }` | `game:{id}` group (`LiveGameHub`) + `tournament:{slug}` group (`TournamentHub` via `IHubContext`) |
| `ClockUpdate` | `{ liveGameId, whiteClockMs, blackClockMs }` | `game:{id}` group (`LiveGameHub`, periodic, every 5s while InProgress) |
| `GameAborted` | `{ liveGameId }` | `game:{id}` group (`LiveGameHub`) + `tournament:{slug}` group (`TournamentHub` via `IHubContext`) |
| `PlayerJoinedGame` | `{ liveGameId, playerId, color }` | `game:{id}` group (`LiveGameHub`, when a participant joins the game room) |
| `PlayerLeftGame` | `{ liveGameId, playerId, color }` | `game:{id}` group (`LiveGameHub`, when a participant leaves the game room) |
| `MoveRejected` | `{ liveGameId, reason }` | **Caller only** via `LiveGameHub` (the player who submitted the invalid move) |

### 4.5 Registration

```csharp
app.MapHub<TournamentHub>("/tournamentHub");
app.MapHub<LiveGameHub>("/liveGameHub");
```

### 4.6 Game Start Flow

```
Black (or Admin)                   Server (LiveGameHub)                 DB / Broadcast
       │                                │                                     │
       │── StartGame(liveGameId) ──────▶│                                     │
       │                                │── Validate: caller is Black or Admin │
       │                                │── Validate: status == NotStarted     │
       │                                │── Set Status = InProgress ──────────▶│ UPDATE LiveGame
       │                                │── Set StartedAt = now ──────────────▶│
       │                                │── Set WhiteClockMs = InitialClockMs ▶│
       │                                │── Set BlackClockMs = InitialClockMs ▶│
       │                                │                                     │
       │◀── GameStarted(id, clocks)    │◀── Broadcast to game:{id} group     │
       │                                │── Start clock timeout watcher        │
```

White's clock begins ticking immediately. The `GetReadyOverlay` on White's client disappears on receiving `GameStarted`.

### 4.7 Move Submission Flow

```
Client (player)                    Server (LiveGameHub)                 DB / Broadcast
       │                                │                                     │
       │── [Optimistically apply move   │                                     │
       │    to local board & clock]     │                                     │
       │                                │                                     │
       │── SubmitMove(gameId,san,clk) ──▶│                                     │
       │                                │── Validate: correct player's turn?   │
       │                                │── Validate: ply == currentPly + 1?   │
       │                                │── Validate: legal SAN move?          │
       │                                │── Cross-check clockMs vs wall-clock  │
       │                                │── Compute server epochMs             │
       │                                │── Build token: "ply:san:clk:epoch"   │
       │                                │── Append "|token" to MoveData ──────▶│ UPDATE LiveGame
       │                                │── Update WhiteClockMs/BlackClockMs ─▶│
       │                                │── Compute new FEN                    │
       │                                │── Check: checkmate/stalemate/draw?   │
       │                                │                                     │
       │◀── MovePlayed(gameId,token,fen)│◀── Broadcast to game:{id} group     │
       │                                │                                     │
       │   [if validation fails]        │                                     │
       │◀── MoveRejected(gameId,reason) │   (sent to caller only)             │
       │── [Revert optimistic move]     │                                     │
       │                                │                                     │
       │   [if game over]               │                                     │
       │◀── GameEnded(gameId,result,..) │── Update Match.Result ─────────────▶│
       │                                │── Update LiveGame.Status ───────────▶│
       │                                │── Notify TournamentHub via           │
       │                                │   IHubContext<TournamentHub> ────────▶│ tournament:{slug}
```

> **Optimistic updates**: The client applies the move to the local board and starts the opponent's clock **immediately** before the server responds. If the server confirms via `MovePlayed`, no further action is needed. If the server rejects via `MoveRejected`, the client reverts the move and shows an error toast.

---

## 5. Move Validation

The server **must validate** every submitted move to prevent illegal states.

| Check | Rule |
|-------|------|
| **Turn order** | Odd ply → White's turn, even ply → Black's turn. Submitting player must match. |
| **Legal move** | The SAN must be a legal move in the current position. Server maintains a board from `MoveData`. |
| **Clock** | Submitted `clockMs` must be ≤ the player's previous remaining clock. Server also cross-checks with wall-clock delta. |
| **Game status** | Game must be `InProgress`. |
| **No duplicate** | The submitted ply must be exactly `currentPly + 1`. |

### 5.1 Server-Side Board State

The server does **not** persist a FEN column. Instead, it reconstructs the board from `MoveData` on each move submission. For a typical game this takes <1ms. The computed FEN is included in the `MovePlayed` broadcast so clients don't need their own chess engine.

> **Optimisation**: For long games, the server can cache the board state in memory (keyed by `liveGameId`). The `MoveData` string is the source of truth; the cache is discardable.

### 5.2 Chess Library

Use a server-side chess library (e.g. [Chess.NET](https://github.com/ProgramFOX/Chess.NET) or a lightweight SAN parser) to:
- Validate move legality
- Detect checkmate, stalemate, threefold repetition, 50-move rule, insufficient material
- Generate FEN for broadcast

---

## 6. Clock Management

### 6.1 Server-Authoritative Clocks

The server is the **authority** on clock values. While the client submits its local `clockMs`, the server also computes the expected remaining time:

```
expectedClockMs = previousClockMs - (currentEpochMs - previousMoveEpochMs)
```

If the client-submitted value diverges by more than **2 seconds** from the server calculation, the server uses its own value (to prevent clock manipulation). A small tolerance accommodates network latency.

### 6.2 Timeout Detection

The server runs a background check (via `IHostedService` or a timer per active game):
- Every second, check if the active player's clock has expired.
- If `remainingMs <= 0`, append a `timeout` token and end the game.

Alternatively, timeout can be detected **lazily** on the next move submission — if the wall-clock delta exceeds the remaining clock, the move is rejected and a timeout is recorded.

### 6.3 Increment / Delay (Future)

The current spec supports **simple time controls** (fixed time per side). The token format can later be extended to support Fischer increment or Bronstein delay by adding an optional increment field:

```
<ply>:<san>:<clockMs>:<epochMs>:<incrementMs>
```

---

## 7. API Endpoints

REST endpoints complement the SignalR hub for non-real-time operations.

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET | `/api/tournaments/{slug}/matches/{matchId}/live` | Get or auto-create a LiveGame for a match. If no LiveGame exists and the caller is a participant of the match, one is created automatically with `Status = NotStarted` and `InitialClockMs` derived from the tournament's `TimeControlMinutes`. Returns the LiveGame state (full MoveData). | Player token (for auto-creation) or None (read-only if exists) |
| GET | `/api/tournaments/{slug}/live-games` | List all live games in a tournament (any status) | None |
| POST | `/api/tournaments/{slug}/matches/{matchId}/live/abort` | Abort a live game | Admin token |

> **Auto-creation**: There is no separate "create" endpoint. Navigating to the live game page triggers a `GET` which creates the `LiveGame` on-demand if one doesn't already exist. This ensures the **LiveGames button is always available** on match cards once the tournament is `InProgress` — players simply click to open the board and the LiveGame is created if needed. Only **one LiveGame per Match** is allowed (unique constraint on `MatchId`).

### 7.1 GET Response DTO

```json
{
  "id": "a1b2c3d4-...",
  "matchId": "e5f6g7h8-...",
  "whitePlayer": { "id": "...", "displayName": "Alice" },
  "blackPlayer": { "id": "...", "displayName": "Bob" },
  "whiteClockMs": 285000,
  "blackClockMs": 291000,
  "initialClockMs": 300000,
  "moveData": "1:e4:300000:1741334400000|2:e5:300000:1741334412000|...",
  "status": "InProgress",
  "currentFen": "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
  "plyCount": 3,
  "startedAt": "2026-03-07T12:00:00Z",
  "completedAt": null
}
```

---

## 8. Client Integration (Vue)

### 8.1 SignalR Connection

The client creates a **separate** SignalR connection to `LiveGameHub` (distinct from the existing `TournamentHub` connection):

```js
import { HubConnectionBuilder } from '@microsoft/signalr'

// Dedicated connection for live game
const gameConnection = new HubConnectionBuilder()
  .withUrl('/liveGameHub')
  .withAutomaticReconnect()
  .build()

await gameConnection.start()

// Join a live game feed
gameConnection.invoke("JoinGame", liveGameId)

// Listen for moves
gameConnection.on("MovePlayed", ({ liveGameId, token, fen }) => {
  // Parse token, update board, animate move
})

// Listen for game end
gameConnection.on("GameEnded", ({ liveGameId, result, reason }) => {
  // Show result overlay
})
```

> **Two connections**: A client viewing a live game will have two SignalR connections — one to `TournamentHub` (for tournament-level events) and one to `LiveGameHub` (for game moves). The `LiveGameHub` connection is created on-demand when entering the live game page and disposed when leaving.

### 8.2 Composable: `useLiveGame`

A Vue composable encapsulates all live game state:

```js
const {
  game,           // reactive LiveGame object
  board,          // current FEN
  moves,          // parsed array of { ply, san, clockMs, epochMs }
  whiteClock,     // reactive countdown (ms)
  blackClock,     // reactive countdown (ms)
  isMyTurn,       // computed: is it the current player's turn?
  submitMove,     // (san) => Promise
  resign,         // () => Promise
  offerDraw,      // () => Promise
  acceptDraw,     // () => Promise
} = useLiveGame(liveGameId, playerToken)
```

### 8.3 Client-Side Clock

The client runs a local `setInterval` (100ms) to tick down the active player's clock for smooth UI updates. On each `MovePlayed` event, the clock is **resynced** to the server-provided values. On each `ClockUpdate` event (every 5s), any drift is corrected.

---

## 9. Parsing Utilities

### 9.1 Server-Side (C#)

```csharp
public static class MoveDataParser
{
    public record MoveToken(int Ply, string San, long ClockMs, long EpochMs);

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
        => string.IsNullOrEmpty(moveData) ? 0 : moveData.Split('|').Length;

    public static bool IsWhiteTurn(string moveData)
        => CurrentPly(moveData) % 2 == 0; // 0 moves played → White's turn (ply 1)
}
```

### 9.2 Client-Side (JS)

```js
export function parseMoveData(moveData) {
  if (!moveData) return []
  return moveData.split('|').map(token => {
    const [ply, san, clockMs, epochMs] = token.split(':')
    return {
      ply: parseInt(ply),
      san,
      clockMs: parseInt(clockMs),
      epochMs: parseInt(epochMs),
    }
  })
}

export function isWhiteTurn(moveData) {
  return parseMoveData(moveData).length % 2 === 0
}
```

---

## 10. Storage Characteristics

| Metric | Value |
|--------|-------|
| Avg token size | ~28 chars (`12:Qxd7+:285000:1741334500000`) |
| Avg game (80 ply) | ~2.3 KB |
| Long game (200 ply) | ~5.8 KB |
| SQLite text column max | 1 GB (no practical limit) |
| 100 concurrent games | ~230 KB total MoveData |

The single-string approach is far more efficient than a `Move` table with 80+ rows per game, both in storage and query count.

---

## 11. Relationship to Existing Match System

The `LiveGame` is an **on-demand companion** to a `Match`. It is created automatically when a player first navigates to the live game page.

| Scenario | How it works |
|----------|---------------|
| **Live game played** | Either player opens the live game page, which auto-creates the LiveGame. Moves are tracked. When the game ends, `Match.Result` is set automatically. |
| **Manual result only** | Players never open the live game page. No LiveGame exists. Players report results via the existing flow. |
| **Hybrid** | A LiveGame is created when a player opens the page, but the players decide to play OTB instead. The organiser aborts the LiveGame and enters the result manually. |

The `Match.Result` is the **authoritative outcome**. The LiveGame merely feeds into it.

---

## 12. Security Considerations

| Concern | Mitigation |
|---------|------------|
| Move tampering | Server validates every move server-side; client-submitted SAN is verified for legality |
| Clock manipulation | Server cross-checks submitted clockMs against wall-clock delta (§6.1) |
| Impersonation | Player token required for SubmitMove/Resign/Draw. The player token is the user's identity — stored as a **hash** in the database and in **plaintext** in the client's `localStorage`. The server resolves the token to a player and their colour in the match, then validates turn order. |
| Spectator interference | Spectators can only call JoinGame/LeaveGame; all mutation methods require auth |
| Replay attacks | Ply number must be exactly `currentPly + 1`; duplicate plies are rejected |

---

## 13. Future Extensions

| Feature | How the encoded string adapts |
|---------|-------------------------------|
| Fischer increment | Add 5th field: `<ply>:<san>:<clockMs>:<epochMs>:<incrementMs>` |
| Move annotations | Add optional 5th field: `<ply>:<san>:<clockMs>:<epochMs>:<annotation>` (e.g. `!`, `??`) |
| Board position snapshots | Periodic FEN checkpoints stored separately for fast mid-game joins |
| PGN export | Parse MoveData → generate standard PGN with `%clk` comments |
| Analysis integration | Feed MoveData to engine (Stockfish WASM) for client-side eval bar |

---

## 14. Reconnection Handling

The server does **not** pause or extend clocks for disconnected players. Reconnection is the client's responsibility.

| Aspect | Behaviour |
|--------|-----------|
| **Clock during disconnection** | Keeps ticking. The server is unaware of (and indifferent to) client connectivity. |
| **Timeout** | A disconnected player will lose on time just like any other timeout (§6.2). There is no grace period. |
| **Reconnect flow** | 1. Client detects SignalR disconnect. 2. Client re-establishes SignalR connection (automatic via HubConnectionBuilder retry policy). 3. Client calls `GET /api/tournaments/{slug}/matches/{matchId}/live` to fetch the full current game state. 4. Client calls `JoinGame(liveGameId)` to re-subscribe to the game group. 5. Client reconciles local state with the fetched state (apply any missed moves). |
| **Missed moves** | Any moves played while the client was disconnected are recovered via the REST fetch. The client replays them locally to catch up. |
| **No server-side tracking** | The server does not track which clients are "connected" for game logic purposes. `PlayerJoinedGame` / `PlayerLeftGame` events are informational only. |

---

## 15. Concurrency

The server uses a **first-write-wins** strategy for move submission. No optimistic concurrency tokens or row versioning is needed.

| Scenario | Handling |
|----------|----------|
| **Duplicate SubmitMove** (e.g. double-click) | The first call succeeds and increments the ply count. The second call fails the "ply must be exactly `currentPly + 1`" check and is rejected. |
| **Simultaneous calls from different clients** | Only one can match the expected next ply; the other is rejected. |
| **No row versioning** | The ply-check acts as a natural concurrency guard — no `RowVersion` / `ConcurrencyToken` column required. |

---

## 16. Summary

The live chess game system is built on three pillars:

1. **One table (`LiveGame`)** — the encoded `MoveData` string contains the entire game history including timecodes, eliminating the need for a normalised moves table.
2. **Dedicated hub (`LiveGameHub`)** — a separate SignalR hub at `/liveGameHub` handles all game-level real-time communication, isolated from the existing `TournamentHub`. SignalR broadcasts individual move tokens (~30 bytes) for real-time updates with minimal bandwidth. Cross-hub notification (e.g. `GameEnded` → tournament group) is handled via `IHubContext<TournamentHub>`.
3. **Server-authoritative validation** — every move is verified for legality, turn order, and clock integrity before being appended and broadcast.

This design keeps the data model minimal, the network payload small, and the implementation straightforward while supporting full real-time spectating and play.
