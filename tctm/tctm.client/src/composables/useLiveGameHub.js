import { ref, readonly } from 'vue'
import * as signalR from '@microsoft/signalr'

/**
 * Composable for the dedicated LiveGameHub SignalR connection.
 * Separate from TournamentHub — connects to /liveGameHub.
 */

/** @type {signalR.HubConnection | null} */
let connection = null

/** @type {import('vue').Ref<string>} */
const status = ref('Disconnected')

/** Currently joined game ID */
let currentGameId = null

/** Auth token passed as query string so the server can identify the caller */
let authToken = null

/** Optional player ID passed as query string for admin-as-player identification */
let authPlayerId = null

/** Registered event handlers: eventName → Set<callback> */
const handlers = new Map()

/**
 * Set the authentication token (player or admin) and optional player ID before connecting.
 * The playerId is needed when an admin is also a match participant so the server can
 * identify them for presence tracking and move submission.
 * Must be called before joinGame / ensureConnected.
 * @param {string|null} token
 * @param {string|null} [playerId]
 */
function setToken(token, playerId = null) {
  // If the token or playerId changed and we already have a connection, tear it down
  // so the next call rebuilds with the new values in the query string.
  if ((authToken !== token || authPlayerId !== playerId) && connection) {
    connection.stop().catch(() => {})
    connection = null
    handlers.clear()
  }
  authToken = token
  authPlayerId = playerId
}

function getConnection() {
  if (connection) return connection

  let hubUrl = `${window.location.origin}/liveGameHub`
  const params = new URLSearchParams()
  if (authToken) params.set('token', authToken)
  if (authPlayerId) params.set('playerId', authPlayerId)
  const qs = params.toString()
  if (qs) hubUrl += `?${qs}`

  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build()

  connection.onreconnecting(() => {
    status.value = 'Reconnecting'
  })

  connection.onreconnected(async () => {
    status.value = 'Connected'
    if (currentGameId) {
      await connection.invoke('JoinGame', currentGameId)
    }
  })

  connection.onclose(() => {
    status.value = 'Disconnected'
  })

  return connection
}

async function ensureConnected() {
  const conn = getConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    status.value = 'Connecting'
    await conn.start()
    status.value = 'Connected'
  }
}

/**
 * Join a live game group for real-time move updates.
 * @param {string} liveGameId
 */
async function joinGame(liveGameId) {
  await ensureConnected()
  if (currentGameId && currentGameId !== liveGameId) {
    try { await connection.invoke('LeaveGame', currentGameId) } catch { /* ignore */ }
  }
  currentGameId = liveGameId
  const result = await connection.invoke('JoinGame', liveGameId)
  return result
}

/**
 * Leave a live game group.
 * @param {string} liveGameId
 */
async function leaveGame(liveGameId) {
  if (!connection) return
  try {
    await connection.invoke('LeaveGame', liveGameId)
  } catch { /* ignore */ }
  if (currentGameId === liveGameId) currentGameId = null
}

/**
 * Submit a move to the server.
 * @param {string} liveGameId
 * @param {string} san
 * @param {number} clockMs
 */
async function submitMove(liveGameId, san, clockMs) {
  await connection.invoke('SubmitMove', liveGameId, san, clockMs)
}

/**
 * Start the game (Black or admin starts White's clock).
 * @param {string} liveGameId
 */
async function startGame(liveGameId) {
  await connection.invoke('StartGame', liveGameId)
}

/**
 * Resign the current game.
 * @param {string} liveGameId
 */
async function resign(liveGameId) {
  await connection.invoke('Resign', liveGameId)
}

/**
 * Offer a draw.
 * @param {string} liveGameId
 */
async function offerDraw(liveGameId) {
  await connection.invoke('OfferDraw', liveGameId)
}

/**
 * Accept a draw offer.
 * @param {string} liveGameId
 */
async function acceptDraw(liveGameId) {
  await connection.invoke('AcceptDraw', liveGameId)
}

/**
 * Abort the game (admin only).
 * @param {string} liveGameId
 */
async function abortGame(liveGameId) {
  await connection.invoke('AbortGame', liveGameId)
}

/**
 * Register a handler for a hub event.
 * Returns an unsubscribe function.
 * @param {string} eventName
 * @param {Function} callback
 * @returns {() => void}
 */
function on(eventName, callback) {
  const conn = getConnection()

  if (!handlers.has(eventName)) {
    handlers.set(eventName, new Set())
    conn.on(eventName, (...args) => {
      for (const cb of handlers.get(eventName)) {
        cb(...args)
      }
    })
  }

  handlers.get(eventName).add(callback)

  return () => {
    handlers.get(eventName)?.delete(callback)
  }
}

/**
 * Stop the connection entirely (cleanup on unmount).
 */
async function disconnect() {
  if (connection) {
    try { await connection.stop() } catch { /* ignore */ }
    connection = null
    currentGameId = null
    handlers.clear()
    status.value = 'Disconnected'
  }
}

export function useLiveGameHub() {
  return {
    status: readonly(status),
    setToken,
    joinGame,
    leaveGame,
    submitMove,
    startGame,
    resign,
    offerDraw,
    acceptDraw,
    abortGame,
    on,
    disconnect,
  }
}
