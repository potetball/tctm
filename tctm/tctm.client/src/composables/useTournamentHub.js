import { ref, readonly } from 'vue'
import * as signalR from '@microsoft/signalr'

/**
 * @typedef {'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'} ConnectionStatus
 */

/** @type {signalR.HubConnection | null} */
let connection = null

/** @type {import('vue').Ref<ConnectionStatus>} */
const status = ref('Disconnected')

/** The slug currently subscribed to. */
let currentSlug = null

/** Registered event handlers: eventName → Set<callback> */
const handlers = new Map()

/**
 * Build (once) and return the shared SignalR connection.
 */
function getConnection() {
  if (connection) return connection

  const hubUrl = `${window.location.origin}/hubs/tournament`

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
    // Re-join the tournament group after reconnect
    if (currentSlug) {
      await connection.invoke('JoinTournament', currentSlug)
    }
  })

  connection.onclose(() => {
    status.value = 'Disconnected'
  })

  return connection
}

/**
 * Start the connection if it isn't already running.
 */
async function ensureConnected() {
  const conn = getConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    status.value = 'Connecting'
    await conn.start()
    status.value = 'Connected'
  }
}

/**
 * Join a tournament group so the client receives real-time updates.
 * If already subscribed to a different tournament, leaves the old one first.
 *
 * @param {string} slug
 */
async function joinTournament(slug) {
  await ensureConnected()

  if (currentSlug && currentSlug !== slug) {
    await connection.invoke('LeaveTournament', currentSlug)
  }

  currentSlug = slug
  await connection.invoke('JoinTournament', slug)
}

/**
 * Leave the current tournament group.
 */
async function leaveTournament() {
  if (!connection || !currentSlug) return
  try {
    await connection.invoke('LeaveTournament', currentSlug)
  } catch {
    // ignore errors when leaving (e.g. connection already closed)
  }
  currentSlug = null
}

/**
 * Register a handler for a hub event.
 * Returns an unsubscribe function.
 *
 * @param {string} eventName
 * @param {Function} callback
 * @returns {() => void} unsubscribe
 */
function on(eventName, callback) {
  const conn = getConnection()

  if (!handlers.has(eventName)) {
    handlers.set(eventName, new Set())

    // Register a single forwarder on the connection that fans-out to all
    // registered callbacks. This avoids issues with SignalR's .off() requiring
    // the exact same function reference.
    conn.on(eventName, (...args) => {
      for (const cb of handlers.get(eventName)) {
        cb(...args)
      }
    })
  }

  handlers.get(eventName).add(callback)

  // Return an unsubscribe function
  return () => {
    handlers.get(eventName)?.delete(callback)
  }
}

/**
 * Composable that exposes the shared SignalR tournament connection.
 *
 * Usage:
 * ```js
 * const { joinTournament, on, status } = useTournamentHub()
 *
 * await joinTournament('tctm-abc123')
 *
 * const unsub = on('PlayerJoined', (player) => { ... })
 * // later: unsub()
 * ```
 */
export function useTournamentHub() {
  return {
    /** Reactive connection status */
    status: readonly(status),
    joinTournament,
    leaveTournament,
    on,
  }
}
