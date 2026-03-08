# Tiny Chess Tournament Manager — Specification

## 1. Overview

**Tiny Chess Tournament Manager (TCTM)** is a lightweight web application that lets a small group of 5–20 people organise and run a chess tournament. A tournament organiser creates a tournament, shares an invite link/code, configures the format and time control, and the system generates the match schedule. Players can play their games directly in the browser via the built-in **Live Game** board, or report results manually. The app enforces chess rules, manages clocks, and tracks standings in real time.

### Tech Stack

| Layer        | Technology                                      |
|--------------|--------------------------------------------------|
| Frontend     | Vue 3 (Vite), Vuetify 4, Tailwind CSS 4         |
| Backend      | ASP.NET Core (C#)                                |
| Chess Engine | C# library (ChessEngine) — move validation, FEN, SAN, endgame detection |
| Database     | SQLite (via EF Core)                             |
| Hosting      | Single-server / Docker-ready                     |

#### Frontend Details

- **Vue 3** — Composition API (`<script setup>`), single-file components
- **Vite 7** — Dev server & build tooling
- **Vuetify 4** — Material Design component library (auto-imported via `vite-plugin-vuetify`)
- **Tailwind CSS 4** — Utility-first CSS framework (integrated via `@tailwindcss/vite`)
- **Material Design Icons** (`@mdi/font`) — Icon set used with Vuetify

---

## 2. User Roles

| Role       | Description |
|------------|-------------|
| **Organiser** | Creates the tournament, configures settings, manages participants, can enter/override results. Identified by a secret admin token generated at tournament creation. |
| **Player**    | Joins via invite link/code, views schedule, reports own match results. No account required — identified by a display name + a per-tournament player token stored in local storage. |

> There is **no user-account system**. Access is controlled entirely through invite codes and tokens.

---

## 3. Tournament Formats

TCTM supports four formats. The organiser picks one when creating a tournament.

### 3.1 Round Robin
- Every player plays every other player exactly once.
- Total rounds: `n − 1` (where _n_ = number of players; a bye is added if _n_ is odd).
- Standings sorted by: points → head-to-head → Sonneborn-Berger.

### 3.2 Swiss System
- A fixed number of rounds (configurable, default `ceil(log₂(n))`).
- Each round, players with similar scores are paired (no repeat pairings).
- Pairing algorithm: simplified Dutch system (pair top-half vs bottom-half within score groups).
- Standings sorted by: points → Buchholz → Sonneborn-Berger.

### 3.3 Single Elimination
- Bracket-style knockout; one loss and you're out.
- Byes are assigned to fill the bracket to the nearest power of 2 (highest seeds get byes).
- Seeding: random by default, or manual seed order set by organiser.

### 3.4 Double Elimination
- Two brackets: Winners and Losers.
- A player must lose twice to be eliminated.
- Grand final between winners-bracket champion and losers-bracket champion.
- If the losers-bracket champion wins the grand final, a single reset match is played.

---

## 4. Time Controls (Game Types)

The organiser selects a time control that applies to **all games** in the tournament. When a match is played via the built-in Live Game board, the server enforces the clock — each player's remaining time is tracked server-side and a background service (`ClockMonitorService`) checks for timeouts every second. If a player's clock reaches zero, the game is automatically decided.

| Preset  | Time per side |
|---------|---------------|
| Bullet  | 1 – 2 min     |
| Blitz   | 3 – 5 min     |
| Rapid   | 10 – 15 min   |

The organiser picks one of the three presets. Within each preset they choose the exact minutes (e.g. Blitz → 3 min).

---

## 5. Core User Flows

### 5.1 Create Tournament (Organiser)
1. Navigate to home page → click **"Create Tournament"**.
2. Enter: tournament name, player count estimate (optional).
3. Select format (Round Robin / Swiss / Single Elimination / Double Elimination).
4. Select time control preset + exact minutes.
5. System generates:
   - A unique **Tournament ID** (short slug, e.g. `tctm-a7k3`).
   - An **Invite Code** (6-character alphanumeric).
   - An **Admin Token** (stored in the organiser's local storage).
6. Organiser is shown the tournament dashboard with a shareable invite link.

### 5.2 Join Tournament (Player)
1. Open invite link **or** go to home page → enter invite code.
2. Enter a **display name** (must be unique within the tournament).
3. System generates a player token → stored in local storage.
4. Player lands on the tournament lobby / schedule.

### 5.3 Start Tournament (Organiser)
1. Organiser reviews the player list on the dashboard.
2. Optionally re-orders players (for seeding in elimination formats).
3. Clicks **"Start Tournament"** → pairings for round 1 are generated.
4. No more players can join after the tournament starts.
5. Show how many rounds is required

### 5.4 Play & Report Results

Results can be determined in two ways: **Live Game** (in-app) or **Manual Reporting**.

#### 5.4a Live Game (in-app play)
1. Either participant navigates to a match and the system auto-creates a **LiveGame** session.
2. Both players join the game room via SignalR (`LiveGameHub`). The page shows a chess board, clocks, and a move list.
3. The **Black player** (or the organiser) starts the game, which begins both clocks.
4. Players submit moves in SAN notation; the server validates each move against the `ChessEngine`, enforces turn order, and manages clock times.
5. The game ends automatically on **checkmate**, **stalemate**, **draw by repetition / fifty-move rule / insufficient material**, **timeout**, **resignation**, or **draw by agreement**.
6. On completion, the match result is recorded and broadcast to the tournament hub.
7. The organiser can **abort** a live game at any time (the match result is left pending).

##### Draw offer flow
- A player may offer a draw **on their own turn**.
- The opponent may accept; if accepted the game ends as a draw.
- Only one draw offer may be pending at a time.

##### Presence tracking
- The server tracks which players are connected to each game room (`GamePresenceTracker`).
- `PlayerJoinedGame` / `PlayerLeftGame` events are broadcast so the UI can indicate readiness.

#### 5.4b Manual Reporting (OTB / external play)
1. Players see the current round's pairings (who plays whom).
2. After a game played externally, **either player** or the **organiser** submits the result:
   - **White wins** (1 – 0)
   - **Black wins** (0 – 1)
   - **Draw** (½ – ½)
3. If the opponent's reported result conflicts, it is flagged for organiser review.
4. Organiser can override any result at any time.

### 5.5 Advance Rounds
- **Round Robin / Swiss**: Once all results for a round are in, the organiser (or auto, if enabled) advances to the next round, generating new pairings.
- **Elimination**: Next-round matchups are automatically determined as results come in.

### 5.6 Complete Tournament
- When the final round concludes, the tournament is marked **Complete**.
- Final standings and (for elimination) the bracket are displayed.
- The tournament remains viewable but no further edits are allowed.

---

## 6. Data Model

### Tournament
| Field            | Type     | Notes |
|------------------|----------|-------|
| Id               | GUID     | PK |
| Slug             | string   | Unique short ID for URLs |
| Name             | string   | Display name |
| InviteCode       | string   | 6-char alphanumeric |
| AdminToken       | string   | Hashed, used to authenticate organiser |
| Format           | enum     | RoundRobin, Swiss, SingleElimination, DoubleElimination |
| TimeControlPreset| enum     | Bullet, Blitz, Rapid |
| TimeControlMinutes| int     | Exact minutes per side |
| Status           | enum     | Lobby, InProgress, Completed |
| CreatedAt        | DateTime | |

### Player
| Field        | Type   | Notes |
|--------------|--------|-------|
| Id           | GUID   | PK |
| TournamentId | GUID   | FK → Tournament |
| DisplayName  | string | Unique per tournament |
| PlayerToken  | string | Hashed |
| Seed         | int?   | Optional manual seed |

### Round
| Field        | Type | Notes |
|--------------|------|-------|
| Id           | GUID | PK |
| TournamentId | GUID | FK → Tournament |
| RoundNumber  | int  | 1-based |
| Status       | enum | Pending, InProgress, Completed |

### Match
| Field         | Type   | Notes |
|---------------|--------|-------|
| Id            | GUID   | PK |
| RoundId       | GUID   | FK → Round |
| WhitePlayerId | GUID?  | FK → Player (null = bye) |
| BlackPlayerId | GUID?  | FK → Player (null = bye) |
| Result        | enum?  | WhiteWin, BlackWin, Draw, null (pending) |
| ReportedBy    | GUID?  | Player or null (organiser) |
| Disputed      | bool   | True if conflicting reports |
| Bracket       | enum?  | Winners, Losers (only for Double Elimination) |

### LiveGame
| Field          | Type       | Notes |
|----------------|------------|-------|
| Id             | GUID       | PK |
| MatchId        | GUID       | FK → Match (one-to-one) |
| WhiteClockMs   | long       | Remaining time for White in milliseconds |
| BlackClockMs   | long       | Remaining time for Black in milliseconds |
| InitialClockMs | long       | Starting clock value (derived from tournament TimeControlMinutes) |
| MoveData       | string     | Pipe-delimited move log (see _Move Data Format_ below) |
| Status         | enum       | NotStarted, InProgress, Completed, Aborted |
| StartedAt      | DateTime?  | UTC timestamp when game started |
| CompletedAt    | DateTime?  | UTC timestamp when game ended |

#### Move Data Format
Moves and control events are stored as a pipe-delimited (`|`) string of tokens. Each token has the format:

```
ply:san:clockMs:epochMs
```

- **ply** — 1-based sequence number.
- **san** — Standard Algebraic Notation for chess moves, or a control keyword: `resign`, `timeout`, `draw-offer`, `draw-accept`, `abort`.
- **clockMs** — the moving player's remaining clock in ms after the move.
- **epochMs** — Unix epoch in ms when the token was recorded.

Example: `1:e4:299200:1709836800000|2:e5:298500:1709836810000`

### Standing (computed view / materialised after each round)
| Field            | Type  | Notes |
|------------------|-------|-------|
| TournamentId     | GUID  | |
| PlayerId         | GUID  | |
| Points           | float | Win=1, Draw=0.5, Loss=0 |
| Wins             | int   | |
| Draws            | int   | |
| Losses           | int   | |
| Buchholz         | float | Swiss tiebreak |
| SonnebornBerger  | float | Round Robin / Swiss tiebreak |

---

## 7. API Endpoints

All endpoints are prefixed with `/api`.

### Tournaments
| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST   | `/tournaments` | Create tournament | None (returns admin token) |
| GET    | `/tournaments/{slug}` | Get tournament details | None |
| POST   | `/tournaments/{slug}/join` | Join via invite code | Invite code in body |
| POST   | `/tournaments/{slug}/start` | Start tournament | Admin token |

### Players
| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET    | `/tournaments/{slug}/players` | List players | None |
| DELETE | `/tournaments/{slug}/players/{id}` | Remove player | Admin token |
| PUT    | `/tournaments/{slug}/players/seed` | Set seed order | Admin token |

### Rounds & Matches
| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET    | `/tournaments/{slug}/rounds` | List rounds with matches | None |
| POST   | `/tournaments/{slug}/rounds/next` | Generate next round | Admin token |
| POST   | `/tournaments/{slug}/matches/{id}/result` | Report result | Player token or admin |
| PUT    | `/tournaments/{slug}/matches/{id}/result` | Override result | Admin token |

### Live Games
| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET    | `/tournaments/{slug}/matches/{id}/live` | Get or auto-create a LiveGame for a match | Player token (query param) |
| GET    | `/tournaments/{slug}/live-games` | List all live games in a tournament | None |
| POST   | `/tournaments/{slug}/matches/{id}/live/abort` | Abort a live game | Admin token |

### Standings
| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET    | `/tournaments/{slug}/standings` | Current standings | None |

---

## 8. Frontend Pages

| Route | Page | Description |
|-------|------|-------------|
| `/` | Home | Create tournament or enter invite code |
| `/t/{slug}` | Tournament Dashboard | Schedule, standings, current round |
| `/t/{slug}/bracket` | Bracket View | Visual bracket (elimination formats) |
| `/t/{slug}/standings` | Standings | Full standings table |
| `/t/{slug}/admin` | Admin Panel | Organiser controls (start, advance, override) |
| `/t/{slug}/game/{matchId}` | Live Game | Interactive chess board with clocks, move list, and game controls |

---

## 9. Non-Functional Requirements

- **No accounts / no external auth** — purely token-based via local storage.
- **Responsive** — usable on mobile (players will be reporting results from their phones).
- **Lightweight** — SQLite database, no external dependencies beyond the .NET runtime.
- **Real-time updates** — two SignalR hubs:
  - `TournamentHub` (`/tournamentHub`) — tournament-level events (player joined, round created, match updated, etc.).
  - `LiveGameHub` (`/liveGameHub`) — game-level events (moves, clocks, draw offers, game ended, player presence). Authenticated via `token` query parameter.
- **Server-side clock enforcement** — a `ClockMonitorService` background service checks for timeouts every second and broadcasts clock sync updates every 5 seconds.
- **Offline-tolerant** — if a player loses connection, SignalR auto-reconnects and re-joins the game/tournament group.

---

## 10. Out of Scope (v1)

- Chess platform API integration for auto-importing results.
- User accounts / persistent profiles across tournaments.
- Elo / rating tracking.
- Spectator chat or messaging.

---

## 11. Milestones

| # | Milestone | Scope |
|---|-----------|-------|
| 1 | **Project scaffold** | Backend API skeleton, EF Core + SQLite, Vue routing, basic layout |
| 2 | **Tournament CRUD** | Create / join / list tournaments, invite codes |
| 3 | **Round Robin engine** | Pairing generation, result reporting, standings |
| 4 | **Swiss engine** | Swiss pairing algorithm + tiebreaks |
| 5 | **Elimination engine** | Single & double elimination brackets |
| 6 | **Real-time updates** | SignalR hub for live score/round changes |
| 7 | **Live Game engine** | In-app chess play: board UI, move validation (ChessEngine), server-side clocks, LiveGameHub, draw/resign/abort flows |
| 8 | **Polish & deploy** | Mobile UI pass, error handling, Docker image |

