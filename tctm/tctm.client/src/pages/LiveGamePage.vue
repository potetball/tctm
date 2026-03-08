<script setup>
/**
 * LiveGamePage.vue — Top-level page orchestrating the live chess game.
 * Route: /t/:slug/game/:matchId
 */
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { liveGames } from '@/api'
import { useLiveGameHub } from '@/composables/useLiveGameHub'
import { useChessEngine } from '@/composables/useChessEngine'

import ChessBoard from '@/components/ChessBoard.vue'
import PlayerBar from '@/components/PlayerBar.vue'
import MoveList from '@/components/MoveList.vue'
import GameControls from '@/components/GameControls.vue'
import PromotionDialog from '@/components/PromotionDialog.vue'
import GetReadyOverlay from '@/components/GetReadyOverlay.vue'

const route = useRoute()
const router = useRouter()
const hub = useLiveGameHub()

// ─── Route params ────────────────────────────────────────────────────────────
const slug = computed(() => route.params.slug)
const matchId = computed(() => route.params.matchId)

// ─── Chess engine ────────────────────────────────────────────────────────────
const engine = useChessEngine()

// ─── Page state ──────────────────────────────────────────────────────────────
const game = ref(null)
const loading = ref(true)
const error = ref('')
const orientation = ref('w')
const selectedSquare = ref(null)
const legalSquares = ref([])
const gameStatus = ref('NotStarted')

// Clocks
const whiteClockMs = ref(0)
const blackClockMs = ref(0)
const initialClockMs = ref(300000)
const activeClockColor = ref(null) // 'w' | 'b' | null

// Players & presence
const whitePlayer = ref(null)
const blackPlayer = ref(null)
const playersPresent = ref(new Set())
const bothPlayersPresent = computed(() => {
  if (!whitePlayer.value || !blackPlayer.value) return false
  return playersPresent.value.has(whitePlayer.value.id) && playersPresent.value.has(blackPlayer.value.id)
})

// Draw offers
const drawOfferedToMe = ref(false)

// Promotion dialog
const showPromotion = ref(false)
const pendingPromotion = ref(null) // { from, to }

// Game end overlay
const showGameEnd = ref(false)
const gameEndResult = ref('')
const gameEndReason = ref('')

// Snackbar for errors
const snackbar = ref(false)
const snackbarText = ref('')

// ─── Identity ────────────────────────────────────────────────────────────────
const localStorageVersion = ref(0)

const myPlayerId = computed(() => {
  localStorageVersion.value
  const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
  return playerData[slug.value]?.playerId || null
})

const myPlayerToken = computed(() => {
  localStorageVersion.value
  const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
  return playerData[slug.value]?.playerToken || null
})

const isAdmin = computed(() => {
  localStorageVersion.value
  const tokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
  return !!tokens[slug.value]
})

const myColor = computed(() => {
  if (!myPlayerId.value) return null
  if (whitePlayer.value?.id === myPlayerId.value) return 'w'
  if (blackPlayer.value?.id === myPlayerId.value) return 'b'
  return null
})

const isPlayer = computed(() => !!myColor.value)
const isBlack = computed(() => myColor.value === 'b')
const isSpectator = computed(() => !isPlayer.value)

const isMyTurn = computed(() => {
  if (!isPlayer.value) return false
  return engine.turn.value === myColor.value
})

const interactive = computed(() => {
  return isPlayer.value && isMyTurn.value && gameStatus.value === 'InProgress'
})

// GetReady overlay:
// - Both players only see it when NotStarted & both players are present
// - White sees "Waiting for opponent to start the clock…"
// - Black sees the "Start Game" button
const showGetReady = computed(() => {
  if (!isPlayer.value || gameStatus.value !== 'NotStarted') return false
  return bothPlayersPresent.value
})

// ─── Board interaction ───────────────────────────────────────────────────────

function onSquareClick({ square }) {
  if (!interactive.value) return

  const piece = getPieceAt(square)

  // If clicking on own piece → select it and show legal moves
  if (piece && piece.color === myColor.value) {
    if (selectedSquare.value === square) {
      // Deselect
      selectedSquare.value = null
      legalSquares.value = []
    } else {
      selectedSquare.value = square
      legalSquares.value = engine.legalMoves(square)
    }
    return
  }

  // If a piece is already selected and clicking a legal destination → move
  if (selectedSquare.value && legalSquares.value.includes(square)) {
    attemptMove(selectedSquare.value, square)
    return
  }

  // Otherwise deselect
  selectedSquare.value = null
  legalSquares.value = []
}

function onPieceDrop({ from, to }) {
  if (!interactive.value) return
  attemptMove(from, to)
}

function onDragStart({ square }) {
  if (!interactive.value) return
  selectedSquare.value = square
  legalSquares.value = engine.legalMoves(square)
}

function onDragEnd() {
  selectedSquare.value = null
  legalSquares.value = []
}

function attemptMove(from, to) {
  const fromPiece = getPieceAt(from)
  if (!fromPiece) return

  // Check if promotion is needed
  const { row } = squareToIndices(to)
  if (fromPiece.type === 'P' && (row === 0 || row === 7)) {
    pendingPromotion.value = { from, to }
    showPromotion.value = true
    return
  }

  executeMove(from, to, null)
}

function onPromotionSelect({ piece }) {
  if (!pendingPromotion.value) return
  const { from, to } = pendingPromotion.value
  pendingPromotion.value = null
  executeMove(from, to, piece)
}

function executeMove(from, to, promotion) {
  const result = engine.makeMove(from, to, promotion)

  selectedSquare.value = null
  legalSquares.value = []

  if (!result) {
    // Illegal move — show error briefly
    showSnackbar('Illegal move')
    return
  }

  // Optimistically update clock: stop my clock, start opponent's
  const myClockRef = myColor.value === 'w' ? whiteClockMs : blackClockMs
  activeClockColor.value = oppositeColor(myColor.value)

  // Submit to server
  hub.submitMove(game.value.id, result.san, myClockRef.value).catch((err) => {
    showSnackbar('Failed to submit move: ' + (err.message || 'Unknown error'))
  })
}

function getPieceAt(square) {
  const { row, col } = squareToIndices(square)
  return engine.board.value[row]?.[col] || null
}

function squareToIndices(sq) {
  const col = sq.charCodeAt(0) - 97
  const row = 8 - parseInt(sq[1])
  return { row, col }
}

function oppositeColor(c) {
  return c === 'w' ? 'b' : 'w'
}

// ─── Data fetching ───────────────────────────────────────────────────────────

async function fetchGame() {
  loading.value = true
  error.value = ''

  try {
    // The server expects the player token as a query parameter for auto-creation
    const params = {}
    if (myPlayerToken.value) params.token = myPlayerToken.value
    else {
      const adminTokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
      if (adminTokens[slug.value]) params.token = adminTokens[slug.value]
    }

    const data = await liveGames.getLiveGame(slug.value, matchId.value, params)
    game.value = data
    gameStatus.value = data.status
    whitePlayer.value = data.whitePlayer
    blackPlayer.value = data.blackPlayer
    whiteClockMs.value = data.whiteClockMs
    blackClockMs.value = data.blackClockMs
    initialClockMs.value = data.initialClockMs

    // Set board orientation
    if (myColor.value) {
      orientation.value = myColor.value
    }

    // Load board state from move data
    if (data.currentFen) {
      engine.loadFen(data.currentFen)
      // Replay move data to get move history
      replayMoveHistory(data.moveData)
    } else if (data.moveData) {
      engine.loadMoveData(data.moveData)
    } else {
      engine.reset()
    }

    // Set active clock
    if (gameStatus.value === 'InProgress') {
      activeClockColor.value = engine.turn.value
    }
  } catch (err) {
    error.value = err.body?.error || err.message || 'Failed to load game.'
  } finally {
    loading.value = false
  }
}

function replayMoveHistory(moveData) {
  if (!moveData) return
  const tokens = moveData.split('|')
  const sans = []
  for (const token of tokens) {
    const parts = token.split(':')
    const san = parts[1]
    if (!['resign', 'timeout', 'draw-offer', 'draw-accept', 'abort'].includes(san)) {
      sans.push(san)
    }
  }
  // Move history is already set by loadFen, but we need the SAN list
  // Reset engine from scratch and replay
  engine.reset()
  for (const san of sans) {
    engine.applySanMove(san)
  }

  // Re-sync clocks from the game data
  if (game.value) {
    whiteClockMs.value = game.value.whiteClockMs
    blackClockMs.value = game.value.blackClockMs
  }
}

// ─── SignalR events ──────────────────────────────────────────────────────────

const unsubs = []

function setupSignalR() {
  if (!game.value) return

  // Provide auth token and player ID so the server can identify this player/admin.
  // When an admin is also a match participant, passing the playerId lets the server
  // associate the admin connection with the correct player for presence and moves.
  const token = myPlayerToken.value
    || (JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')[slug.value])
    || null
  hub.setToken(token, myPlayerId.value)

  // Join the game room and seed presence from the server's response,
  // which includes all player IDs already in the room.
  hub.joinGame(game.value.id).then((result) => {
    if (result?.presentPlayerIds?.length) {
      playersPresent.value = new Set([...playersPresent.value, ...result.presentPlayerIds])
    }
  })

  // Also pre-populate our own presence in case the server response is slow
  if (myPlayerId.value) {
    playersPresent.value = new Set([...playersPresent.value, myPlayerId.value])
  }

  unsubs.push(hub.on('MovePlayed', ({ token, fen }) => {
    if (!token) return
    const parts = token.split(':')
    const san = parts[1]
    const clockMs = parseInt(parts[2])
    const ply = parseInt(parts[0])

    // Skip control tokens
    if (['resign', 'timeout', 'draw-offer', 'draw-accept', 'abort'].includes(san)) return

    // Determine who moved: odd ply = white, even ply = black
    const moverColor = ply % 2 === 1 ? 'w' : 'b'

    // If this is our own move confirmation, just sync clocks
    if (moverColor === myColor.value) {
      if (moverColor === 'w') whiteClockMs.value = clockMs
      else blackClockMs.value = clockMs
      activeClockColor.value = oppositeColor(moverColor)
      // Clear any draw offer when a move is played
      drawOfferedToMe.value = false
      return
    }

    // Opponent's move — apply it
    engine.applySanMove(san)

    // Update clocks
    if (moverColor === 'w') whiteClockMs.value = clockMs
    else blackClockMs.value = clockMs
    activeClockColor.value = oppositeColor(moverColor)
    drawOfferedToMe.value = false
  }))

  unsubs.push(hub.on('GameStarted', ({ whiteClockMs: wClock, blackClockMs: bClock }) => {
    gameStatus.value = 'InProgress'
    whiteClockMs.value = wClock
    blackClockMs.value = bClock
    activeClockColor.value = 'w' // White moves first
  }))

  unsubs.push(hub.on('GameEnded', ({ result, reason }) => {
    gameStatus.value = 'Completed'
    activeClockColor.value = null
    gameEndResult.value = result || ''
    gameEndReason.value = reason || ''
    showGameEnd.value = true
  }))

  unsubs.push(hub.on('GameAborted', () => {
    gameStatus.value = 'Aborted'
    activeClockColor.value = null
    gameEndResult.value = 'Game Aborted'
    gameEndReason.value = 'abort'
    showGameEnd.value = true
  }))

  unsubs.push(hub.on('DrawOffered', ({ offeredBy }) => {
    // If the draw was offered by the opponent, show accept/decline
    if (offeredBy !== myPlayerId.value) {
      drawOfferedToMe.value = true
    }
  }))

  unsubs.push(hub.on('MoveRejected', ({ reason }) => {
    engine.undoLastMove()
    // Restore active clock
    activeClockColor.value = engine.turn.value
    showSnackbar(`Move rejected: ${reason}`)
  }))

  unsubs.push(hub.on('ClockUpdate', ({ whiteClockMs: wClock, blackClockMs: bClock }) => {
    // Smooth correction: if drift > 1s, snap
    const wDrift = Math.abs(whiteClockMs.value - wClock)
    const bDrift = Math.abs(blackClockMs.value - bClock)
    if (wDrift > 1000) whiteClockMs.value = wClock
    if (bDrift > 1000) blackClockMs.value = bClock
  }))

  unsubs.push(hub.on('PlayerJoinedGame', ({ playerId }) => {
    playersPresent.value = new Set([...playersPresent.value, playerId])
  }))

  unsubs.push(hub.on('PlayerLeftGame', ({ playerId }) => {
    const next = new Set(playersPresent.value)
    next.delete(playerId)
    playersPresent.value = next
  }))
}

function cleanupSignalR() {
  unsubs.forEach(fn => fn())
  unsubs.length = 0
  if (game.value) {
    hub.leaveGame(game.value.id)
  }
}

// ─── Game controls ───────────────────────────────────────────────────────────

async function onStartGame() {
  if (!game.value) return
  try {
    await hub.startGame(game.value.id)
  } catch (err) {
    showSnackbar('Failed to start game: ' + (err.message || ''))
  }
}

async function onResign() {
  if (!game.value) return
  try {
    await hub.resign(game.value.id)
  } catch (err) {
    showSnackbar('Failed to resign: ' + (err.message || ''))
  }
}

async function onOfferDraw() {
  if (!game.value) return
  try {
    await hub.offerDraw(game.value.id)
    showSnackbar('Draw offer sent')
  } catch (err) {
    showSnackbar('Failed to offer draw: ' + (err.message || ''))
  }
}

async function onAcceptDraw() {
  if (!game.value) return
  try {
    await hub.acceptDraw(game.value.id)
  } catch (err) {
    showSnackbar('Failed to accept draw: ' + (err.message || ''))
  }
  drawOfferedToMe.value = false
}

function onDeclineDraw() {
  drawOfferedToMe.value = false
}

async function onAbortGame() {
  if (!game.value) return
  try {
    await hub.abortGame(game.value.id)
  } catch (err) {
    showSnackbar('Failed to abort game: ' + (err.message || ''))
  }
}

function onFlipBoard() {
  orientation.value = orientation.value === 'w' ? 'b' : 'w'
}

function onBackToTournament() {
  router.push({ name: 'tournament', params: { slug: slug.value } })
}

function showSnackbar(text) {
  snackbarText.value = text
  snackbar.value = true
}

// ─── Captured pieces ─────────────────────────────────────────────────────────

const capturedByWhite = computed(() => engine.captured.value.capturedByWhite)
const capturedByBlack = computed(() => engine.captured.value.capturedByBlack)
const whiteMaterialAdv = computed(() => {
  const adv = engine.captured.value.whiteMaterialAdvantage
  return adv > 0 ? adv : 0
})
const blackMaterialAdv = computed(() => {
  const adv = -engine.captured.value.whiteMaterialAdvantage
  return adv > 0 ? adv : 0
})

// ─── Opponent / current player bars ──────────────────────────────────────────

const topPlayer = computed(() => {
  return orientation.value === 'w' ? blackPlayer.value : whitePlayer.value
})
const bottomPlayer = computed(() => {
  return orientation.value === 'w' ? whitePlayer.value : blackPlayer.value
})
const topColor = computed(() => orientation.value === 'w' ? 'b' : 'w')
const bottomColor = computed(() => orientation.value === 'w' ? 'w' : 'b')
const topClockMs = computed(() => orientation.value === 'w' ? blackClockMs.value : whiteClockMs.value)
const bottomClockMs = computed(() => orientation.value === 'w' ? whiteClockMs.value : blackClockMs.value)
const topClockActive = computed(() => activeClockColor.value === topColor.value && gameStatus.value === 'InProgress')
const bottomClockActive = computed(() => activeClockColor.value === bottomColor.value && gameStatus.value === 'InProgress')
const topCaptured = computed(() => topColor.value === 'b' ? capturedByBlack.value : capturedByWhite.value)
const bottomCaptured = computed(() => bottomColor.value === 'w' ? capturedByWhite.value : capturedByBlack.value)
const topMaterialAdv = computed(() => topColor.value === 'b' ? blackMaterialAdv.value : whiteMaterialAdv.value)
const bottomMaterialAdv = computed(() => bottomColor.value === 'w' ? whiteMaterialAdv.value : blackMaterialAdv.value)

// ─── Game end result display ─────────────────────────────────────────────────

function gameEndTitle() {
  if (gameEndReason.value === 'abort') return 'Game Aborted'
  const res = gameEndResult.value
  if (res === 'WhiteWin') return 'White wins!'
  if (res === 'BlackWin') return 'Black wins!'
  if (res === 'Draw') return 'Draw'
  return res || 'Game Over'
}

function gameEndSubtitle() {
  const r = gameEndReason.value
  if (r === 'checkmate') return 'by checkmate'
  if (r === 'resign') return 'by resignation'
  if (r === 'timeout') return 'on time'
  if (r === 'draw-accept') return 'by agreement'
  if (r === 'stalemate') return 'by stalemate'
  if (r === 'abort') return 'The game was aborted by the organiser'
  return r || ''
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────

onMounted(async () => {
  await fetchGame()
  if (!error.value) {
    setupSignalR()
  }
})

onBeforeUnmount(() => {
  cleanupSignalR()
})

// If route changes (different match), reload
watch([slug, matchId], async () => {
  cleanupSignalR()
  await fetchGame()
  if (!error.value) setupSignalR()
})
</script>

<template>
  <v-container style="max-width: 960px; margin: 0 auto;">
    <!-- Loading -->
    <div v-if="loading" class="text-center py-16">
      <v-progress-circular indeterminate color="amber-darken-2" size="64" />
    </div>

    <!-- Error -->
    <v-alert v-else-if="error" type="error" variant="tonal" class="my-8">
      {{ error }}
      <template #append>
        <v-btn variant="text" @click="onBackToTournament">Back to Tournament</v-btn>
      </template>
    </v-alert>

    <!-- Game layout -->
    <template v-else-if="game">
      <!-- Connection banner -->
      <v-banner
        v-if="hub.status.value === 'Reconnecting'"
        color="warning"
        icon="mdi-wifi-off"
        density="compact"
        class="mb-2"
      >
        Connection lost — reconnecting…
      </v-banner>

      <div class="game-layout">
        <!-- Main area: board + move list -->
        <div class="game-main">
          <!-- Top player bar (opponent / top side) -->
          <PlayerBar
            :player="topPlayer || { displayName: '?' }"
            :color="topColor"
            :clock-ms="topClockMs"
            :clock-active="topClockActive"
            :initial-clock-ms="initialClockMs"
            :captured-pieces="topCaptured"
            :material-advantage="topMaterialAdv"
            :is-current-user="topPlayer?.id === myPlayerId"
            class="mb-2"
          />

          <!-- Board container (for overlay positioning) -->
          <div class="board-container" style="position: relative;">
            <ChessBoard
              :board="engine.board.value"
              :orientation="orientation"
              :interactive="interactive"
              :my-color="myColor"
              :last-move="engine.lastMove.value"
              :selected-square="selectedSquare"
              :legal-squares="legalSquares"
              :check-square="engine.checkSquare.value"
              @square-click="onSquareClick"
              @piece-drop="onPieceDrop"
              @drag-start="onDragStart"
              @drag-end="onDragEnd"
            />

            <!-- Get Ready overlay for players -->
            <GetReadyOverlay :show="showGetReady" :my-color="myColor" @start-game="onStartGame" />

            <!-- Game End overlay -->
            <Transition name="fade">
              <div v-if="showGameEnd" class="game-end-overlay">
                <div class="game-end-card">
                  <div class="text-h5 font-weight-bold">{{ gameEndTitle() }}</div>
                  <div class="text-body-2 text-medium-emphasis mt-1">{{ gameEndSubtitle() }}</div>
                  <v-btn
                    color="amber-darken-2"
                    class="mt-4"
                    rounded="lg"
                    @click="showGameEnd = false"
                  >
                    Dismiss
                  </v-btn>
                </div>
              </div>
            </Transition>
          </div>

          <!-- Bottom player bar (current player / bottom side) -->
          <PlayerBar
            :player="bottomPlayer || { displayName: '?' }"
            :color="bottomColor"
            :clock-ms="bottomClockMs"
            :clock-active="bottomClockActive"
            :initial-clock-ms="initialClockMs"
            :captured-pieces="bottomCaptured"
            :material-advantage="bottomMaterialAdv"
            :is-current-user="bottomPlayer?.id === myPlayerId"
            class="mt-2"
          />

          <!-- Game controls -->
          <GameControls
            :game-status="gameStatus"
            :is-player="isPlayer"
            :is-admin="isAdmin"
            :is-my-turn="isMyTurn"
            :draw-offered="drawOfferedToMe"
            :is-black="isBlack"
            :both-players-present="bothPlayersPresent"
            @start-game="onStartGame"
            @resign="onResign"
            @offer-draw="onOfferDraw"
            @accept-draw="onAcceptDraw"
            @decline-draw="onDeclineDraw"
            @abort-game="onAbortGame"
            @flip-board="onFlipBoard"
            @back-to-tournament="onBackToTournament"
          />
        </div>

        <!-- Side panel: move list (desktop) -->
        <div class="game-sidebar">
          <v-card variant="outlined" rounded="xl" class="pa-3">
            <div class="text-subtitle-2 font-weight-bold mb-2">
              <v-icon icon="mdi-format-list-numbered" size="small" class="mr-1" />
              Moves
            </div>
            <MoveList
              :moves="engine.moveHistory.value"
              :current-ply="engine.moveHistory.value.length"
            />
          </v-card>

          <!-- Status info -->
          <v-card variant="outlined" rounded="xl" class="pa-3 mt-3">
            <div class="text-caption text-medium-emphasis">
              <div>Status: <strong>{{ gameStatus }}</strong></div>
              <div>Turn: <strong>{{ engine.turn.value === 'w' ? 'White' : 'Black' }}</strong></div>
              <div v-if="engine.isCheck.value" class="text-red font-weight-bold">Check!</div>
              <div v-if="engine.isCheckmate.value" class="text-red font-weight-bold">Checkmate!</div>
              <div v-if="engine.isStalemate.value" class="font-weight-bold">Stalemate</div>
              <div v-if="isSpectator" class="mt-2">
                <v-icon icon="mdi-eye-outline" size="small" class="mr-1" />
                Spectating
              </div>
            </div>
          </v-card>
        </div>
      </div>

      <!-- Move list for mobile (below board) -->
      <div class="game-sidebar-mobile mt-3">
        <v-expansion-panels variant="accordion">
          <v-expansion-panel title="Moves">
            <v-expansion-panel-text>
              <MoveList
                :moves="engine.moveHistory.value"
                :current-ply="engine.moveHistory.value.length"
              />
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </div>
    </template>

    <!-- Promotion dialog -->
    <PromotionDialog
      v-model:show="showPromotion"
      :color="myColor || 'w'"
      @promotion-select="onPromotionSelect"
    />

    <!-- Snackbar for errors / notifications -->
    <v-snackbar v-model="snackbar" :timeout="3000" color="red-darken-2">
      {{ snackbarText }}
      <template #actions>
        <v-btn variant="text" @click="snackbar = false">Close</v-btn>
      </template>
    </v-snackbar>
  </v-container>
</template>

<style scoped>
.game-layout {
  display: flex;
  gap: 24px;
  align-items: flex-start;
}

.game-main {
  flex: 1;
  min-width: 0;
}

.game-sidebar {
  width: 260px;
  flex-shrink: 0;
}

.game-sidebar-mobile {
  display: none;
}

.board-container {
  position: relative;
}

/* Game end overlay */
.game-end-overlay {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
  border-radius: 4px;
}

.game-end-card {
  background: #1e1e2e;
  border-radius: 16px;
  padding: 2rem;
  text-align: center;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
}

/* Responsive */
@media (max-width: 768px) {
  .game-layout {
    flex-direction: column;
    padding: 0;
    margin: 0 -16px;
    width: calc(100% + 32px);
  }

  .game-main {
    max-width: 100vw;
    width: 100%;
  }

  .game-sidebar {
    display: none;
  }

  .game-sidebar-mobile {
    display: block;
  }
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.4s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
