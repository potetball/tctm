# Front End Chessboard Specification

Front end specification of a minimal chess board in JS/Vue in conjunction with `livechessgamespecification.md` in order for users to play matches against each other.

---

## 1. Background

When the tournament has started, you (as the player) are presented with an additional button (**LiveGame**) on your match card. This navigates you to your 1-on-1 game with the opponent. In this screen you see:

- A chessboard with your opponent's pieces at the **top** and yours at the **bottom** (the board auto-orients based on your colour).
- Player info bars showing display name + clock for each side.
- When both players have joined the game room, **Black** (or the admin) presses **Start Game** — just as in over-the-board chess where Black starts White's clock. White sees a **"Get Ready"** card overlaying the board while waiting for Black to start. The overlay disappears the moment Black presses Start Game, White's clock begins ticking, and the game is underway.

---

## 2. Technical Principles

| Principle | Detail |
|-----------|--------|
| **Minimal dependencies** | No external chessboard library. The board is a simple 8×8 CSS grid rendered in Vue. |
| **Chess emoji pieces** | Use Unicode chess symbols (♔♕♖♗♘♙♚♛♜♝♞♟) for rendering. No images or icon fonts required. |
| **Client-side validation** | A lightweight JS chess engine validates moves locally before sending to the server, reducing unnecessary network requests and enabling move highlighting. |
| **Drag and drop** | Pieces are moved via HTML5 drag-and-drop (or pointer events for mobile). When a piece is picked up, legal destination squares are highlighted. |
| **Click-click alternative** | Tap a piece to select it (highlights legal moves), then tap a destination square to move. Supports mobile and accessibility. |
| **SignalR real-time sync** | Moves are sent/received through the existing `TournamentHub` SignalR connection (see `livechessgamespecification.md` §4). |
| **Orientation** | The board is always shown from the current player's perspective. Spectators see the board from White's perspective by default with a flip button. |

---

## 3. Chess Logic Module — `useChessEngine.js`

A composable that manages the client-side board state. This does **not** duplicate the server — it provides instant feedback while the server remains authoritative.

### 3.1 Responsibilities

- Maintain an 8×8 board array from a FEN string.
- Parse SAN moves and update the board.
- Generate all legal moves for the current position (for highlighting).
- Detect check, checkmate, stalemate, draw conditions (informational only — server is authoritative).
- Support special moves: castling (kingside/queenside), en passant, pawn promotion.

### 3.2 Board Representation

```js
/**
 * Internal board state: 8×8 array of piece objects or null.
 * Piece object: { type: 'K'|'Q'|'R'|'B'|'N'|'P', color: 'w'|'b' }
 *
 * board[0][0] = a8 (top-left from White's perspective)
 * board[7][7] = h1 (bottom-right from White's perspective)
 */
```

### 3.3 Public API

```js
const {
  board,            // Ref<(Piece | null)[][]> — reactive 8×8 board state
  fen,              // Ref<string> — current FEN
  turn,             // Ref<'w' | 'b'> — whose turn it is
  isCheck,          // Ref<boolean>
  isCheckmate,      // Ref<boolean>
  isStalemate,      // Ref<boolean>
  isDraw,           // Ref<boolean>
  moveHistory,      // Ref<string[]> — SAN moves played
  legalMoves,       // (square: string) => string[] — legal destination squares for a piece
  makeMove,         // (from: string, to: string, promotion?: string) => { san: string } | null
  loadFen,          // (fen: string) => void
  loadMoveData,     // (moveData: string) => void — replay from encoded move string
  reset,            // () => void — reset to starting position
  undoLastMove,     // () => void — undo (for client-side optimistic revert on server rejection)
} = useChessEngine()
```

### 3.4 FEN Parsing / Generation

The engine must be able to:
- Parse a FEN string into the internal board representation (used when joining a game mid-progress).
- Generate the current FEN (for display/debug, but the server's FEN is authoritative).

### 3.5 Move Validation Rules

The engine validates all standard chess rules client-side:

| Rule | Detail |
|------|--------|
| **Piece movement** | Each piece type has its own movement pattern (sliding, jumping, pawn rules). |
| **Obstruction** | Sliding pieces (R, B, Q) cannot jump over other pieces. |
| **Pin detection** | A piece pinned to the king cannot move in a way that exposes the king. |
| **Check escape** | When in check, only moves that resolve the check are legal. |
| **Castling** | King and rook must not have moved; squares between must be empty; king must not pass through or land on an attacked square. |
| **En passant** | Track the en-passant target square from the previous move. |
| **Pawn promotion** | When a pawn reaches the 8th rank, the player must choose Q, R, B, or N. |

---

## 4. Vue Components

### 4.1 Component Tree

```
LiveGamePage.vue
├── PlayerBar.vue              (opponent — top)
│   ├── Display name
│   ├── Captured pieces
│   └── ChessClock.vue
├── ChessBoard.vue (wrapper with relative positioning)
│   ├── BoardSquare.vue × 64
│   ├── PromotionDialog.vue
│   └── GetReadyOverlay.vue    (shown to White when NotStarted & both players present)
├── PlayerBar.vue              (current player — bottom)
│   ├── Display name
│   ├── Captured pieces
│   └── ChessClock.vue
├── MoveList.vue
└── GameControls.vue
```

### 4.2 `LiveGamePage.vue`

The top-level page component. Responsible for orchestrating the game.

| Responsibility | Detail |
|----------------|--------|
| **Route** | `/t/:slug/game/:matchId` |
| **Fetch game state** | On mount, call `GET /api/tournaments/{slug}/matches/{matchId}/live` to get the `LiveGameDto`. |
| **SignalR subscription** | Join the `game:{liveGameId}` group via `connection.invoke('JoinGame', liveGameId)`. |
| **Board orientation** | Determine if the current player is White or Black (from `myPlayerId` vs `whitePlayer.id` / `blackPlayer.id`). Flip the board accordingly. |
| **Move submission** | When the player makes a move on the board, call `connection.invoke('SubmitMove', liveGameId, san, clockMs)`. |
| **Move reception** | On `MovePlayed` event, parse the token, update the chess engine, animate the move. |
| **Player presence** | Track which players have joined the game room via `PlayerJoinedGame` / `PlayerLeftGame` events. Expose `bothPlayersPresent` computed. |
| **Get Ready overlay** | When the current player is White, the game is `NotStarted`, and both players are present, show a "Get Ready" card overlaying the board. The overlay disappears on `GameStarted`. |
| **Game lifecycle** | Handle `GameStarted`, `GameEnded`, `GameAborted`, `DrawOffered` events. |
| **Cleanup** | On unmount, call `connection.invoke('LeaveGame', liveGameId)`. |

### 4.3 `ChessBoard.vue`

The 8×8 grid that renders the board and handles piece interaction.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `board` | `(Piece \| null)[][]` | The 8×8 board state from the chess engine |
| `orientation` | `'w' \| 'b'` | Which colour is at the bottom |
| `interactive` | `boolean` | Whether the player can move pieces (false when not their turn, or game over, or spectating) |
| `lastMove` | `{ from: string, to: string } \| null` | Highlight the last move played |
| `selectedSquare` | `string \| null` | Currently selected square |
| `legalSquares` | `string[]` | Squares to highlight as legal destinations |
| `checkSquare` | `string \| null` | King square to highlight in red when in check |

**Emits:**

| Event | Payload | Description |
|-------|---------|-------------|
| `square-click` | `{ square: string }` | A square was clicked |
| `piece-drop` | `{ from: string, to: string }` | A piece was dragged and dropped |
| `promotion-select` | `{ piece: 'Q' \| 'R' \| 'B' \| 'N' }` | Player chose a promotion piece |

**Rendering:**

```
Ranks are rendered top-to-bottom.
If orientation = 'w': rank 8 at top, rank 1 at bottom, files a→h left-to-right.
If orientation = 'b': rank 1 at top, rank 8 at bottom, files h→a left-to-right.
```

**Styling:**

| Element | Style |
|---------|-------|
| Light squares | `bg-amber-100` (Tailwind) |
| Dark squares | `bg-amber-800` |
| Selected square | `ring-2 ring-blue-500` overlay |
| Legal move dot | Small centered circle on empty legal squares, ring on occupied legal squares (capture) |
| Last move highlight | `bg-yellow-300/40` overlay on from and to squares |
| Check highlight | `bg-red-500/40` overlay on the king square |

**Piece Emoji Map:**

```js
const PIECE_EMOJI = {
  K: { w: '♔', b: '♚' },
  Q: { w: '♕', b: '♛' },
  R: { w: '♖', b: '♜' },
  B: { w: '♗', b: '♝' },
  N: { w: '♘', b: '♞' },
  P: { w: '♙', b: '♟' },
}
```

Pieces are rendered as large emoji text (e.g. `text-4xl` / `text-5xl`) centered in the square. The `cursor-grab` class is applied when interactive and it's the player's piece.

### 4.4 `BoardSquare.vue`

A single square on the chessboard.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `piece` | `Piece \| null` | The piece on this square (or null) |
| `square` | `string` | Algebraic notation (e.g. `e4`) |
| `isLight` | `boolean` | Light or dark square |
| `isSelected` | `boolean` | Whether this square is currently selected |
| `isLegal` | `boolean` | Whether this is a legal move destination |
| `isLastMove` | `boolean` | Part of the last-move highlight |
| `isCheck` | `boolean` | King in check on this square |
| `interactive` | `boolean` | Whether pieces can be moved |
| `draggable` | `boolean` | Whether the piece on this square can be dragged |

**Behaviour:**

- Click → emit `square-click`.
- Drag start → set the piece data in the drag event; apply `opacity-50` to the source square.
- Drag over legal square → show drop indicator.
- Drop → emit `piece-drop` with `{ from, to }`.

### 4.5 `PromotionDialog.vue`

Shown when a pawn reaches the promotion rank.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `color` | `'w' \| 'b'` | The colour of the promoting pawn |
| `show` | `boolean` | v-model for visibility |

**Display:** A small floating panel (Vuetify `v-menu` or `v-dialog`) with four clickable emoji options: ♕ ♖ ♗ ♘ (or ♛ ♜ ♝ ♞ for Black). Emits the chosen piece type.

### 4.6 `GetReadyOverlay.vue`

A translucent overlay card shown to White while waiting for Black to start the game. Positioned absolutely over the `ChessBoard.vue` container.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `show` | `boolean` | Whether the overlay is visible |

**Display:**

```
┌─────────────────────────────────────┐
│          (dimmed board behind)      │
│    ┌─────────────────────────┐      │
│    │         ♔               │      │
│    │                         │      │
│    │      Get Ready          │      │
│    │                         │      │
│    │  Waiting for your       │      │
│    │  opponent to start      │      │
│    │  the clock…             │      │
│    └─────────────────────────┘      │
└─────────────────────────────────────┘
```

**Styling:**

| Element | Style |
|---------|-------|
| Overlay backdrop | `absolute inset-0 bg-black/50 flex items-center justify-center z-10` |
| Card | `bg-white rounded-xl shadow-2xl p-8 text-center max-w-xs` |
| Crown icon | White king emoji `♔` in `text-6xl` |
| Title | `text-2xl font-bold mt-4` — "Get Ready" |
| Subtitle | `text-sm text-gray-500 mt-2` — "Waiting for your opponent to start the clock…" with a pulsing animation |
| Transition | `v-fade-transition` — smooth fade-out when Black starts the game |

**Behaviour:**

- Shown only to White when `gameStatus === 'NotStarted'` and both players are present in the game room.
- Disappears with a smooth fade-out transition when the `GameStarted` event is received.
- The board is visible but dimmed behind the overlay, giving White a preview of the starting position.
- Not shown to Black, spectators, or admins.

### 4.7 `ChessClock.vue`

Displays a ticking clock for one player.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `remainingMs` | `number` | Remaining time in milliseconds |
| `active` | `boolean` | Whether this clock is currently ticking |
| `initialMs` | `number` | Starting time (for progress bar) |

**Behaviour:**

- When `active` is true, a `requestAnimationFrame` loop decrements the displayed time locally every frame for smooth ticking.
- Display format: `M:SS` when ≥ 1 minute, `S.t` (seconds + tenths) when < 10 seconds.
- Visual urgency: the clock turns **red** when < 30 seconds remaining, and **pulses** when < 10 seconds.
- The clock does **not** manage the authoritative time — it's a display-only component. The actual remaining time is synced via `ClockUpdate` events and `MovePlayed` tokens from SignalR.

### 4.8 `PlayerBar.vue`

Displays player information above/below the board.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `player` | `{ id, displayName }` | Player info |
| `color` | `'w' \| 'b'` | Which colour this player is |
| `clockMs` | `number` | Remaining clock time |
| `clockActive` | `boolean` | Whether their clock is ticking |
| `initialClockMs` | `number` | Starting time |
| `capturedPieces` | `string[]` | List of piece types captured by this player |
| `materialAdvantage` | `number` | Point advantage (positive = ahead) |
| `isCurrentUser` | `boolean` | Highlight if this is the logged-in player |

**Display:**

```
┌──────────────────────────────────────────┐
│  ♟♟♞  Alice              │   4:32   │
└──────────────────────────────────────────┘
```

- Captured pieces shown as small emojis on the left.
- Material advantage shown as `+N` if ahead.
- Clock on the right, styled via `ChessClock.vue`.
- If `isCurrentUser`, the bar has a subtle highlighted border.

### 4.9 `MoveList.vue`

Displays the move history in standard chess notation.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `moves` | `string[]` | SAN move history from the chess engine |
| `currentPly` | `number` | The current ply being viewed (for future move navigation) |

**Display:**

A scrollable list of moves in two-column format:

```
 1. e4    e5
 2. Nf3   Nc6
 3. Bb5   ...
```

- The latest move is auto-scrolled into view.
- Moves are displayed in a compact `v-table` or simple `<div>` grid.
- Check (`+`) and checkmate (`#`) annotations are included in the SAN.

### 4.10 `GameControls.vue`

Action buttons below the board for the playing user.

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `gameStatus` | `string` | `NotStarted`, `InProgress`, `Completed`, `Aborted` |
| `isPlayer` | `boolean` | Whether the current user is a participant |
| `isAdmin` | `boolean` | Whether the current user is the admin |
| `isMyTurn` | `boolean` | Whether it's the current user's turn |
| `drawOffered` | `boolean` | Whether a draw has been offered to this player |
| `isBlack` | `boolean` | Whether the current user is playing Black |
| `bothPlayersPresent` | `boolean` | Whether both players have joined the game room |

**Buttons:**

| Button | Condition | Action |
|--------|-----------|--------|
| **Start Game** | `gameStatus === 'NotStarted'` and `(isBlack \|\| isAdmin)` and both players present | Invoke `StartGame` on the hub. Per chess convention, Black starts White's clock. |
| **Resign** | `gameStatus === 'InProgress'` and `isPlayer` | Confirm dialog → invoke `Resign` |
| **Offer Draw** | `gameStatus === 'InProgress'` and `isPlayer` and `isMyTurn` | Invoke `OfferDraw` |
| **Accept Draw** | `drawOffered === true` | Invoke `AcceptDraw` |
| **Decline Draw** | `drawOffered === true` | Dismiss the draw offer locally (no hub call — the offer auto-expires server-side when the opponent makes a move) |
| **Abort Game** | `isAdmin` and `gameStatus !== 'Completed'` | Confirm dialog → invoke `AbortGame` |
| **Flip Board** | Always | Toggle board orientation |
| **Back to Tournament** | Always | `router.push({ name: 'tournament', params: { slug } })` |

---

## 5. Routing

### 5.1 New Route

Add to `router/index.js`:

```js
{
  path: '/t/:slug/game/:matchId',
  name: 'live-game',
  component: () => import('@/pages/LiveGamePage.vue'),
}
```

### 5.2 Navigation

- From the **TournamentDashboard**, each match card shows a **"Live Game"** button when:
  - The tournament is `InProgress`.
- Clicking the button navigates to `/t/{slug}/game/{matchId}`.
- On mount, the `LiveGamePage` calls `GET /api/tournaments/{slug}/matches/{matchId}/live`, which **auto-creates** the `LiveGame` if one doesn't exist and the caller is a match participant (see `livechessgamespecification.md` §7).
- Spectators can also navigate to any live game to watch.

---

## 6. API Integration

### 6.1 New API Module — `api/liveGames.js`

```js
import { get, post } from './httpClient'

/** GET /api/tournaments/{slug}/matches/{matchId}/live
 *  Returns the LiveGame state. Auto-creates the LiveGame if the caller is a
 *  match participant and no LiveGame exists yet. */
export function getLiveGame(slug, matchId) {
  return get(`/tournaments/${slug}/matches/${matchId}/live`)
}

/** GET /api/tournaments/{slug}/live-games */
export function listLiveGames(slug) {
  return get(`/tournaments/${slug}/live-games`)
}

/** POST /api/tournaments/{slug}/matches/{matchId}/live/abort */
export function abortLiveGame(slug, matchId, data) {
  return post(`/tournaments/${slug}/matches/${matchId}/live/abort`, data)
}
```

### 6.2 SignalR Extension — `useTournamentHub.js`

The existing composable is extended with game-specific methods:

```js
/** Join a live game group for real-time move updates. */
async function joinGame(liveGameId) {
  await ensureConnected()
  await connection.invoke('JoinGame', liveGameId)
}

/** Leave a live game group. */
async function leaveGame(liveGameId) {
  if (!connection) return
  try {
    await connection.invoke('LeaveGame', liveGameId)
  } catch { /* ignore */ }
}

/** Submit a move to the server. */
async function submitMove(liveGameId, san, clockMs) {
  await connection.invoke('SubmitMove', liveGameId, san, clockMs)
}

/** Resign the current game. */
async function resign(liveGameId) {
  await connection.invoke('Resign', liveGameId)
}

/** Offer a draw. */
async function offerDraw(liveGameId) {
  await connection.invoke('OfferDraw', liveGameId)
}

/** Accept a draw offer. */
async function acceptDraw(liveGameId) {
  await connection.invoke('AcceptDraw', liveGameId)
}

/** Start the game (begin clocks). */
async function startGame(liveGameId) {
  await connection.invoke('StartGame', liveGameId)
}

/** Abort the game (admin only). */
async function abortGame(liveGameId) {
  await connection.invoke('AbortGame', liveGameId)
}
```

---

## 7. Game Flow — Client-Side State Machine

### 7.1 States

When the game is `NotStarted` and both players have joined the game room:
- **White** sees a **"Get Ready"** overlay card centered on the chessboard. The overlay reads _"Waiting for your opponent to start the clock…"_ with a subtle pulsing animation. The board is visible but dimmed behind the overlay. White cannot interact with the board.
- **Black** sees the **"Start Game"** button in the Game Controls area. Only Black (or the admin) can press it — mirroring over-the-board chess where Black starts White's clock.
- When Black presses **Start Game**, the server broadcasts `GameStarted`. White's overlay disappears, White's clock begins ticking, and the game is underway.

```
┌─────────────┐  StartGame (Black)  ┌─────────────┐
│  NotStarted │ ───────────────────▶│ InProgress  │
└─────────────┘                     └──────┬──────┘
                                        │
                        ┌───────────────┼───────────────┐
                        ▼               ▼               ▼
                  ┌──────────┐   ┌──────────┐   ┌──────────┐
                  │ Checkmate│   │  Resign  │   │ Timeout  │
                  └────┬─────┘   └────┬─────┘   └────┬─────┘
                       │              │               │
                       ▼              ▼               ▼
                  ┌─────────────────────────────────────┐
                  │            Completed                │
                  └─────────────────────────────────────┘
                        ▲               ▲
                  ┌─────┴─────┐   ┌─────┴─────┐
                  │ Draw      │   │  Abort    │
                  │ (accepted)│   │  (admin)  │
                  └───────────┘   └───────────┘
```

### 7.2 Client-Side Move Flow

```
1. Player picks up / clicks a piece
2. useChessEngine.legalMoves(square) → highlight destinations
3. Player drops / clicks destination
4. If promotion → show PromotionDialog, wait for choice
5. useChessEngine.makeMove(from, to, promotion?)
   → returns { san } or null (illegal)
6. If null → shake animation, do nothing
7. Optimistically update the board
8. connection.invoke('SubmitMove', liveGameId, san, localClockMs)
9. Wait for 'MovePlayed' event from server
   a. If token matches our move → confirmed, no further action
   b. If 'MoveRejected' event received → undo the optimistic move, show error toast
10. On opponent's 'MovePlayed' → parse token, animate piece, update clocks
```

### 7.3 Clock Synchronisation

```
On each 'MovePlayed' event:
  - Extract clockMs from the token
  - Update the corresponding player's clock
  - Start ticking the other player's clock locally

On each 'ClockUpdate' event (every 5s):
  - Correct any drift between local clock and server clock
  - Apply smooth interpolation (don't jump abruptly)

On 'GameEnded' with reason 'timeout':
  - Stop all clocks
  - Show timeout indicator
```

---

## 8. Responsive Layout

### 8.1 Desktop (≥ 768px)

```
┌─────────────────────────────────────────────────────┐
│                    Opponent Bar                      │
├───────────────────────────────┬─────────────────────┤
│                               │                     │
│                               │    Move List        │
│         Chessboard            │    1. e4   e5       │
│          (480px)              │    2. Nf3  Nc6      │
│                               │    ...              │
│                               │                     │
├───────────────────────────────┴─────────────────────┤
│                     Player Bar                       │
├─────────────────────────────────────────────────────┤
│                   Game Controls                      │
└─────────────────────────────────────────────────────┘
```

### 8.2 Mobile (< 768px)

```
┌─────────────────────┐
│    Opponent Bar      │
├─────────────────────┤
│                     │
│     Chessboard      │
│   (100vw - 16px)    │
│                     │
├─────────────────────┤
│     Player Bar      │
├─────────────────────┤
│   Game Controls     │
├─────────────────────┤
│    Move List        │
│   (collapsible)     │
└─────────────────────┘
```

The board always renders as a **perfect square**. On mobile it takes the full viewport width (minus padding). On desktop it is capped at 480px–560px.

---

## 9. Animations & UX Polish

| Feature | Implementation |
|---------|---------------|
| **Piece movement** | CSS `transition: transform 0.2s ease` on piece elements when a move is received from the opponent. |
| **Legal move dots** | Small semi-transparent circles on empty squares; semi-transparent rings on capturable squares. Appear on piece selection/drag-start. |
| **Drag ghost** | The browser's native drag image is used (the emoji). Add `cursor-grabbing` on drag. |
| **Last move highlight** | A semi-transparent yellow overlay on the from/to squares of the last move. |
| **Check indicator** | Red radial gradient overlay on the king's square when in check. |
| **Capture animation** | Brief scale-up + fade-out on the captured piece's square. |
| **Illegal move shake** | A quick horizontal shake animation (`@keyframes shake`) if the player tries an illegal move. |
| **Get Ready overlay** | A centered card over the board shown to White when `NotStarted` and both players are present. Reads "Get Ready — Waiting for your opponent to start the clock…" with a pulsing subtitle. Fades out on `GameStarted`. |
| **Game end overlay** | A centered overlay on the board showing the result (e.g. "Checkmate — White wins", "Draw by agreement") with a dismiss button. |
| **Sound effects** | Optional (future). Move sound, capture sound, check sound. Can be toggled off. |

---

## 10. Spectator Mode

When a user navigates to a live game they are **not** a participant in, the page enters spectator mode:

| Aspect | Behaviour |
|--------|-----------|
| **Board** | Non-interactive (no drag/drop, no click-to-move). Default White orientation with a flip button. |
| **Clocks** | Visible and ticking, synchronised via SignalR. |
| **Move list** | Visible, auto-scrolling. |
| **Controls** | Only "Flip Board" and "Back to Tournament" are shown. |
| **Admin spectating** | Admin additionally sees the "Abort Game" button. |

---

## 11. Error Handling

| Scenario | Handling |
|----------|----------|
| **Move rejected by server** | On `MoveRejected` event: revert optimistic board update, show a `v-snackbar` error toast with the rejection reason (e.g. "Illegal move", "Not your turn"). |
| **SignalR disconnection** | Show a `v-banner` at the top: "Connection lost — reconnecting…". The clock **keeps ticking** server-side during disconnection. On reconnect, re-join the game group and fetch the latest game state via `GET /api/tournaments/{slug}/matches/{matchId}/live` to catch up on any missed moves. If the player's time expired while disconnected, they will see a `GameEnded` with reason `timeout`. |
| **Game not found** | Show a full-page error message with a "Back to Tournament" link. |
| **Stale game state** | If the ply in a received `MovePlayed` event doesn't match `currentPly + 1`, fetch the full game state via REST and resync. |
| **Clock desync** | If local clock diverges from server `ClockUpdate` by > 1 second, snap to server value with a smooth transition. |

---

## 12. File Structure

```
src/
├── api/
│   └── liveGames.js             # REST client for live game endpoints
├── composables/
│   ├── useChessEngine.js         # Client-side chess logic (board, moves, validation)
│   └── useTournamentHub.js       # Extended with game-specific SignalR methods
├── components/
│   ├── ChessBoard.vue            # 8×8 board grid
│   ├── BoardSquare.vue           # Single square component
│   ├── ChessClock.vue            # Ticking clock display
│   ├── GameControls.vue          # Action buttons (resign, draw, etc.)
│   ├── GetReadyOverlay.vue       # "Get Ready" overlay for White before game starts
│   ├── MoveList.vue              # Move history display
│   ├── PlayerBar.vue             # Player info + clock bar
│   └── PromotionDialog.vue       # Pawn promotion picker
└── pages/
    └── LiveGamePage.vue          # Top-level game page
```

---

## 13. Captured Pieces & Material Count

Track captured pieces by diffing the starting material against the current board state:

```js
const STARTING_MATERIAL = { P: 8, N: 2, B: 2, R: 2, Q: 1, K: 1 }

// For each colour, count pieces currently on the board.
// captured = STARTING_MATERIAL[type] - currentCount[type]
```

Display captured pieces grouped by type in descending value order (Q, R, B, N, P) as small emojis. Show material advantage as `+N` (where N = point difference using standard values: P=1, N=3, B=3, R=5, Q=9).

---

## 14. Accessibility

| Feature | Implementation |
|---------|---------------|
| **Keyboard navigation** | Arrow keys to move between squares, Enter to select/place, Escape to deselect. |
| **ARIA labels** | Each square has `aria-label="e4, white pawn"` (or `"e4, empty"`). |
| **Screen reader announcements** | On move played, announce via `aria-live="polite"`: "White plays e4" / "Black plays knight to f6, check". |
| **High contrast** | Ensure sufficient contrast between light/dark squares and piece colours. The emoji pieces inherently have good contrast. |
| **Focus indicators** | Visible focus ring on the currently focused square during keyboard navigation. |

---

## 15. Future Enhancements (Out of Scope for v1)

- **Move timestamps in move list** — show time spent per move.
- **Pre-moves** — queue a move before it's your turn.
- **Board themes** — selectable colour schemes for the board.
- **Piece sets** — alternative piece styles (SVG sets instead of emoji).
- **Analysis mode** — step through moves after game completion.
- **Sound effects** — move, capture, check, game-end sounds.
- **Draw by repetition / 50-move** — auto-detect and offer draw.
- **Opening book display** — show the opening name (e.g. "Italian Game").