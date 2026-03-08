import { ref } from 'vue'

const STORAGE_KEY = 'tctm_tournaments'

/**
 * @typedef {Object} KnownTournament
 * @property {string} slug
 * @property {string} name
 * @property {'admin' | 'player' | 'spectator'} role
 */

/** @type {import('vue').Ref<KnownTournament[]>} */
const tournaments = ref(loadFromStorage())

function loadFromStorage() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]')
  } catch {
    return []
  }
}

function persist() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(tournaments.value))
}

/**
 * Add or update a known tournament.
 * Role priority: admin > player > spectator (won't downgrade).
 */
function addTournament(slug, name, role = 'spectator') {
  const rolePriority = { admin: 3, player: 2, spectator: 1 }
  const idx = tournaments.value.findIndex((t) => t.slug === slug)

  if (idx >= 0) {
    const existing = tournaments.value[idx]
    // Update name if it changed
    existing.name = name || existing.name
    // Only upgrade role, never downgrade
    if (rolePriority[role] > rolePriority[existing.role]) {
      existing.role = role
    }
  } else {
    tournaments.value.push({ slug, name, role })
  }

  persist()
}

/**
 * Remove a tournament from the known list.
 * This is client-only — does NOT leave the tournament on the server.
 */
function removeTournament(slug) {
  tournaments.value = tournaments.value.filter((t) => t.slug !== slug)
  persist()
}

/**
 * Get a specific known tournament by slug, or null.
 */
function getTournament(slug) {
  return tournaments.value.find((t) => t.slug === slug) || null
}

export function useTournamentStore() {
  return {
    /** Reactive list of all known tournaments */
    tournaments,
    addTournament,
    removeTournament,
    getTournament,
  }
}
